using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace FirstGame.Editor
{
    public static class CloudCacheDiagnostics
    {
        // Read the same cache path displayed by Unity's Preferences/GI Cache UI.
        public static void Log()
        {
            var property=typeof(Lightmapping).GetProperty("diskCachePath",BindingFlags.Static|BindingFlags.NonPublic);
            if(property==null)throw new InvalidOperationException("Unity GI cache path diagnostic API is unavailable.");
            string path=(string)property.GetValue(null);
            string requested=Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if(!string.IsNullOrEmpty(requested)&&!Path.GetFullPath(path).StartsWith(Path.GetFullPath(requested)+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                throw new InvalidOperationException("Unity did not honor the configured XDG cache root: "+path);
            Debug.Log("FIRSTGAME_CLOUD_GI_CACHE_PATH "+path);
        }
    }
}
