using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ResourceManagement
{
    public class ResourceLoader
    {
        protected AssetBundle mainPack;
        protected AssetBundleManifest manifest;
        protected bool hasInit = false;
        protected Dictionary<string, EnhancedAssetBundle> packageMap = new();
        protected string currentPlatform;
        public IEnumerable<AssetBundle> AssetBundles => packageMap.Values.Select(dep => dep.AssetBundle);

        public ResourceLoader()
        {
            InitPlatform();
        }

        public void Init()
        {
            if (!hasInit)
            {
                mainPack = AssetBundle.LoadFromFile($"{Application.streamingAssetsPath}/{currentPlatform}");
                manifest = mainPack.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                hasInit = true;
            }
        }

        protected virtual string GetAssetBundlePath(string assetBundleName)
        {
            return $"{Application.streamingAssetsPath}/{assetBundleName}";
        }

        protected virtual void InitPlatform()
        {
            currentPlatform = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "PC",
                RuntimePlatform.Android => "Android",
                RuntimePlatform.WebGLPlayer => "Web",
                RuntimePlatform.IPhonePlayer => "IOS",
                _ => "Other",
            };
        }

        #region LoadAssetBundle

        public EnhancedAssetBundle LoadSingleAssetBundle(string assetBundleName)
        {
            string assetBundlePath = GetAssetBundlePath(assetBundleName);
            EnhancedAssetBundle enhancedAssetBundle = packageMap.GetValueOrDefault(assetBundlePath);
            if (enhancedAssetBundle == null)
            {
                AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
                enhancedAssetBundle = new EnhancedAssetBundle(assetBundle);
                packageMap.Add(assetBundlePath, enhancedAssetBundle);
            }
            return enhancedAssetBundle;
        }

        public EnhancedAssetBundle LoadAssetBundle(string assetBundleName)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadSingleAssetBundle(assetBundleName);

            string[] dependencies = manifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length != 0)
            {
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependencyName = dependencies[i];
                    EnhancedAssetBundle dependency = LoadSingleAssetBundle(dependencyName);
                    enhancedAssetBundle.AddDependcy(dependency.AssetBundle);
                }
            }

            return enhancedAssetBundle;
        }

        #endregion


        #region LoadAssetBundle -- Async

        public void LoadAssetBundleAsync(string assetBundleName, MonoBehaviour invoker, System.Action<AssetBundle> callback)
        {
            invoker.StartCoroutine(LoadSingleAssetBundleCoroutine(assetBundleName, callback));
        }

        private IEnumerator LoadSingleAssetBundleCoroutine(string assetBundleName, System.Action<AssetBundle> callback)
        {
            yield return null;
            string assetBundlePath = GetAssetBundlePath(assetBundleName);
            EnhancedAssetBundle enhancedAssetBundle = packageMap.GetValueOrDefault(assetBundlePath);
            if (enhancedAssetBundle == null)
            {
                AssetBundleCreateRequest assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(assetBundlePath);
                yield return assetBundleCreateRequest;
                AssetBundle assetBundle = assetBundleCreateRequest.assetBundle;
                enhancedAssetBundle = new EnhancedAssetBundle(assetBundle);
                packageMap.Add(assetBundlePath, enhancedAssetBundle);
            }

            string[] dependencies = manifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length != 0)
            {
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependencyName = dependencies[i];
                    string dependencyPath = GetAssetBundlePath(dependencyName);
                    EnhancedAssetBundle dependency = packageMap.GetValueOrDefault(dependencyPath);
                    if (dependency == null)
                    {
                        AssetBundleCreateRequest assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(dependencyPath);
                        yield return assetBundleCreateRequest;
                        AssetBundle assetBundle = assetBundleCreateRequest.assetBundle;
                        dependency = new EnhancedAssetBundle(assetBundle);
                        packageMap.Add(dependencyPath, dependency);
                    }
                    enhancedAssetBundle.AddDependcy(dependency.AssetBundle);
                }
            }

            callback(enhancedAssetBundle.AssetBundle);
        }

        #endregion


        #region UnLoad

        public void UnLoad(string assetBundleName, bool unLoadDependencies = false)
        {
            var path = GetAssetBundlePath(assetBundleName);
            if (packageMap.ContainsKey(path))
            {
                EnhancedAssetBundle enhancedAssetBundle = packageMap[path];
                enhancedAssetBundle.AssetBundle.Unload(false);
                packageMap.Remove(path);
                if (unLoadDependencies)
                {
                    foreach (var ab in enhancedAssetBundle.DependencyByAssetBundles)
                    {
                        var name = ab.name;
                        path = GetAssetBundlePath(name);

                        if (packageMap.ContainsKey(path))
                        {
                            var dependency = packageMap[path];
                            dependency.AssetBundle.Unload(false);
                            packageMap.Remove(path);
                        }
                    }
                }
            }
        }

        public void UnLoad(EnhancedAssetBundle enhancedAssetBundle, bool unLoadDependencies = false)
        {
            if (!enhancedAssetBundle.Check()) return;

            var name = enhancedAssetBundle.AssetBundle.name;
            var path = GetAssetBundlePath(name);
            if (packageMap.ContainsKey(path))
            {
                enhancedAssetBundle.AssetBundle.Unload(false);
                packageMap.Remove(path);
                if (unLoadDependencies)
                {
                    foreach (var ab in enhancedAssetBundle.DependencyByAssetBundles)
                    {
                        name = ab.name;
                        path = GetAssetBundlePath(name);

                        if (packageMap.ContainsKey(path))
                        {
                            var dependcy = packageMap[path];
                            dependcy.AssetBundle.Unload(false);
                            packageMap.Remove(path);
                        }
                    }
                }
            }
        }

        public void UnLoadAll()
        {
            AssetBundle.UnloadAllAssetBundles(false);
            packageMap.Clear();
        }

        #endregion


        #region LoadResource

        public T LoadResource<T>(string assetBundleName, string resourceName, bool unLoadAssetBundle = false, bool unLoadWhole = false) where T : UnityEngine.Object
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            T resource = enhancedAssetBundle.AssetBundle.LoadAsset<T>(resourceName);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resource;
        }

        public Object LoadResource(string assetBundleName, string resourceName, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            Object resource = enhancedAssetBundle.AssetBundle.LoadAsset(resourceName);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resource;
        }

        public Object LoadResource(string assetBundleName, string resourceName, System.Type type, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            Object resource = enhancedAssetBundle.AssetBundle.LoadAsset(resourceName, type);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resource;
        }

        #endregion


        #region LoadResource -- Async

        public void LoadResourceAsync<T>(string assetBundleName, string resourceName, MonoBehaviour invoker, System.Action<T> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false) where T : Object
        {
            invoker.StartCoroutine(LoadResourceCoroutine(assetBundleName, resourceName, callback, unLoadAssetBundle, unLoadWhole));
        }

        public void LoadResourceAsync(string assetBundleName, string resourceName, MonoBehaviour invoker, System.Action<Object> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            invoker.StartCoroutine(LoadResourceCoroutine(assetBundleName, resourceName, callback, unLoadAssetBundle, unLoadWhole));
        }

        public void LoadResourceAsync(string assetBundleName, string resourceName, System.Type type, MonoBehaviour invoker, System.Action<Object> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            invoker.StartCoroutine(LoadResourceCoroutine(assetBundleName, resourceName, type, callback, unLoadAssetBundle, unLoadWhole));
        }

        private IEnumerator LoadResourceCoroutine<T>(string assetBundleName, string resourceName, System.Action<T> callback, bool unLoadAssetBundle, bool unLoadWhole) where T : Object
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            AssetBundleRequest assetBundleRequest = assetBundle.LoadAssetAsync<T>(resourceName);
            yield return assetBundleRequest;
            T resource = (T)assetBundleRequest.asset;
            callback(resource);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
        }

        private IEnumerator LoadResourceCoroutine(string assetBundleName, string resourceName, System.Type type, System.Action<Object> callback, bool unLoadAssetBundle, bool unLoadWhole)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            AssetBundleRequest assetBundleRequest = assetBundle.LoadAssetAsync(resourceName, type);
            yield return assetBundleRequest;
            Object resource = assetBundleRequest.asset;
            callback(resource);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
        }

        #endregion


        #region LoadAllResource

        public T[] LoadAllResource<T>(string assetBundleName, bool unLoadAssetBundle = false, bool unLoadWhole = false) where T : Object
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            T[] resources = assetBundle.LoadAllAssets<T>();
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resources;
        }

        public Object[] LoadAllResource(string assetBundleName, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            Object[] resources = assetBundle.LoadAllAssets();
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resources;
        }

        public Object[] LoadAllResource(string assetBundleName, System.Type type, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            Object[] resources = assetBundle.LoadAllAssets(type);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
            return resources;
        }

        #endregion


        #region LoadAllResource -- Async

        public void LoadAllResourceAsync<T>(string assetBundleName, MonoBehaviour invoker, System.Action<T[]> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false) where T : Object
        {
            invoker.StartCoroutine(LoadAllResourceCoroutine(assetBundleName, callback, unLoadAssetBundle, unLoadWhole));
        }

        public void LoadAllResourceAsync(string assetBundleName, MonoBehaviour invoker, System.Action<Object[]> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            invoker.StartCoroutine(LoadAllResourceCoroutine(assetBundleName, callback, unLoadAssetBundle, unLoadWhole));
        }

        public void LoadAllResourceAsync(string assetBundleName, System.Type type, MonoBehaviour invoker, System.Action<Object[]> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false)
        {
            invoker.StartCoroutine(LoadAllResourceCoroutine(assetBundleName, type, callback, unLoadAssetBundle, unLoadWhole));
        }

        public IEnumerator LoadAllResourceCoroutine<T>(string assetBundleName, System.Action<T[]> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false) where T : Object
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            AssetBundleRequest assetBundleRequest = assetBundle.LoadAllAssetsAsync<T>();
            yield return assetBundleRequest;
            T[] resources = assetBundleRequest.allAssets.Cast<T>().ToArray();
            callback(resources);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
        }

        public IEnumerator LoadAllResourceCoroutine(string assetBundleName, System.Type type, System.Action<Object[]> callback, bool unLoadAssetBundle = false, bool unLoadWhole = false) 
        {
            EnhancedAssetBundle enhancedAssetBundle = LoadAssetBundle(assetBundleName);
            AssetBundle assetBundle = enhancedAssetBundle.AssetBundle;
            AssetBundleRequest assetBundleRequest = assetBundle.LoadAllAssetsAsync(type);
            yield return assetBundleRequest;
            Object[] resources = assetBundleRequest.allAssets.ToArray();
            callback(resources);
            if (unLoadAssetBundle)
            {
                UnLoad(assetBundleName, unLoadWhole);
            }
        }

        #endregion

    }
}