using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace FirstGame.Editor
{
    public static class BuildSourceBinding
    {
        [Serializable] public sealed class Entry { public string file,sha256; public long bytes; }
        [Serializable] public sealed class Inputs { public string game,startedUtc,fingerprint,scope;public Entry[] files; }
        public static string Capture(string output,string game)
        {
            var rows=new List<Entry>();
            foreach(string folder in new[]{"Assets","Packages","ProjectSettings"})
            foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
            {
                string local=path.Replace('\\','/');
                if(local=="Assets/Scenes/Boot.unity"||local=="Assets/Resources/Fonts/Korean.asset"||local=="Assets/Resources/Fonts/Display.asset"||(local.StartsWith("Assets/Resources/Data/Cards/",StringComparison.Ordinal)&&Path.GetExtension(local)==".asset"))continue;
                var bytes=File.ReadAllBytes(path);
                rows.Add(new Entry{file="Games/"+game+"/"+local,bytes=bytes.LongLength,sha256=Hash(bytes)});
            }
            if(game=="PathOfDawn")foreach(string relative in new[]{"Tools/Art/GospelAuthoredProps/material-spec.json","Tools/Art/GospelWell/material-spec.json"})
            {
                var bytes=File.ReadAllBytes(Path.GetFullPath("../../"+relative));
                rows.Add(new Entry{file=relative,bytes=bytes.LongLength,sha256=Hash(bytes)});
            }
            var files=rows.OrderBy(r=>r.file,StringComparer.Ordinal).ToArray();
            string text=string.Concat(files.Select(r=>r.file+"\0"+r.bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\0"+r.sha256+"\n"));
            var value=new Inputs{game=game,startedUtc=DateTime.UtcNow.ToString("o"),fingerprint=Hash(Encoding.UTF8.GetBytes(text)),files=files,
                scope="Every project input except regenerated Boot/card-authoring/TMP-cache files; full generators/stable metadata/runtime catalog/art are bound. CardAssets separately semantically audited."};
            Directory.CreateDirectory(output);string manifest=Path.Combine(output,"BuildInputs.json");
            File.WriteAllText(manifest,JsonUtility.ToJson(value,true));
            return value.fingerprint;
        }
        static string Hash(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
