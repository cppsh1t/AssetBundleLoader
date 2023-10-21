using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ResourceManagement
{
    public class EnhancedAssetBundle
    {
        internal AssetBundle AssetBundle { get; private set; }

        internal ICollection<AssetBundle> DependencyByAssetBundles {get; private set;} = new HashSet<AssetBundle>();

        internal bool HasDependcyBy => DependencyByAssetBundles.Count > 0;

        internal EnhancedAssetBundle(AssetBundle assetBundle)
        {
            AssetBundle = assetBundle;
        }

        public void AddDependcy(AssetBundle assetBundle)
        {
            DependencyByAssetBundles.Add(assetBundle);
        }

        public bool Check()
        {
            try 
            {
                var name = AssetBundle.name;
                return true;
            }
            catch (MissingReferenceException) 
            {
                return false;
            }
        }
    }
}