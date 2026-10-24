// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static partial class EngineMigratorBridge
{
    static void ExportTexturePng(string input,TextureImporter importer,string output)
    {
        if(Path.GetExtension(input).Equals(".jpg",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(input).Equals(".jpeg",StringComparison.OrdinalIgnoreCase))
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,!importer.sRGBTexture);
            try{if(!ImageConversion.LoadImage(image,File.ReadAllBytes(input),false))throw new Exception("JPEG decode failed: "+input);File.WriteAllBytes(output,image.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(image);}
            return;
        }
        // Only the private project copy is reimported. Always restore its settings.
        var readable=importer.isReadable;var compression=importer.textureCompression;
        try{importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();var image=AssetDatabase.LoadAssetAtPath<Texture2D>(input);var png=image.EncodeToPNG();if(png==null||png.Length==0)throw new Exception("TGA PNG conversion failed: "+input);File.WriteAllBytes(output,png);}
        finally{importer.isReadable=readable;importer.textureCompression=compression;importer.SaveAndReimport();}
    }
}
