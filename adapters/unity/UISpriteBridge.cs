using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.UI;
public static partial class EngineMigratorBridge {
 static readonly Dictionary<string,Sprite> ImportedUISprites=new Dictionary<string,Sprite>();
 static string PersistedResourceId(UnityEngine.Object asset){var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset));const string prefix="EngineMigratorSourceId=";var value=importer&&!(asset is Sprite&&importer is TextureImporter)&&importer.userData.StartsWith(prefix,StringComparison.Ordinal)?importer.userData.Substring(prefix.Length):Id(asset);if(!System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9_]+$"))throw new Exception("Invalid persisted UI resource identity");return value;}
 static string ExportUITexture(Texture texture,EMPackage package){
  var image=texture as Texture2D;if(!image){package.blockers.Add("UI Texture2D 외 텍스처 미지원");return "";}var path=AssetDatabase.GetAssetPath(image);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
  if(!importer||!Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)||(importer.textureType!=TextureImporterType.Default&&importer.textureType!=TextureImporterType.Sprite)||importer.wrapModeU==TextureWrapMode.MirrorOnce||importer.wrapModeV==TextureWrapMode.MirrorOnce||importer.mipMapBias!=0){package.blockers.Add(path+": 기본 PNG UI 텍스처 import 설정 외 미지원");return "";}
  var bytes=File.ReadAllBytes(path);if(bytes.Length<24||ReadPngInt(bytes,16)!=image.width||ReadPngInt(bytes,20)!=image.height){package.blockers.Add(path+": 리사이즈된 UI 텍스처 미지원");return "";}
  var id=PersistedResourceId(image);if(!package.textures.Any(t=>t.id==id)){var data=new EMTexture{id=id,name=image.name,file="textures/"+id+".png",srgb=importer.sRGBTexture,filter=importer.filterMode.ToString(),wrapU=importer.wrapModeU.ToString(),wrapV=importer.wrapModeV.ToString(),mipmaps=importer.mipmapEnabled};Directory.CreateDirectory(Path.Combine(PackageRoot,"textures"));File.Copy(path,Path.Combine(PackageRoot,data.file),true);package.textures.Add(data);}return id;
 }
 static int ReadPngInt(byte[] bytes,int i)=>(bytes[i]<<24)|(bytes[i+1]<<16)|(bytes[i+2]<<8)|bytes[i+3];
 static void ExportUISprite(Image image,EMUI data,EMPackage package){
  if(image.preserveAspect||image.useSpriteMesh||image.alphaHitTestMinimumThreshold!=0)package.blockers.Add(image.name+": 비율 유지·스프라이트 메시·알파 히트 테스트 미지원");
  var sprite=image.overrideSprite;if(image.sprite!=sprite){package.blockers.Add(image.name+": 별도의 overrideSprite 상태 변환은 미지원입니다");return;}if(!sprite)return;
  if(sprite.packed||sprite.border!=Vector4.zero||UnityEngine.Sprites.DataUtility.GetPadding(sprite)!=Vector4.zero){package.blockers.Add(sprite.name+": 패킹·트리밍·슬라이스 스프라이트 미지원");return;}
  var rect=sprite.rect;data.sprite=PersistedResourceId(sprite);data.spriteName=sprite.name;data.spriteRect=new Vector4(rect.x,rect.y,rect.width,rect.height);data.spritePivot=new Vector2(sprite.pivot.x/rect.width,sprite.pivot.y/rect.height);data.pixelsPerUnit=sprite.pixelsPerUnit;
  data.textureWidth=sprite.texture.width;data.textureHeight=sprite.texture.height;data.uv=new Vector4(rect.x/data.textureWidth,rect.y/data.textureHeight,rect.width/data.textureWidth,rect.height/data.textureHeight);data.texture=ExportUITexture(sprite.texture,package);
 }
 static Sprite ImportUISprite(EMUI data,Dictionary<string,Texture2D> textures,string root){
  if(string.IsNullOrEmpty(data.sprite))return null;Sprite sprite;if(ImportedUISprites.TryGetValue(data.sprite,out sprite)){
   var rect=sprite.rect;var pivot=new Vector2(sprite.pivot.x/rect.width,sprite.pivot.y/rect.height);
   if(!textures.ContainsKey(data.texture)||sprite.texture!=textures[data.texture]||(new Vector4(rect.x,rect.y,rect.width,rect.height)-data.spriteRect).sqrMagnitude>1e-6||(pivot-data.spritePivot).sqrMagnitude>1e-8||Mathf.Abs(sprite.pixelsPerUnit-data.pixelsPerUnit)>1e-5||sprite.name!=data.spriteName)throw new Exception("Conflicting shared UI sprite identity: "+data.sprite);
   return sprite;
  }
  if(!textures.ContainsKey(data.texture))throw new Exception("UI sprite texture reference missing");var r=data.spriteRect;sprite=Sprite.Create(textures[data.texture],new Rect(r.x,r.y,r.z,r.w),data.spritePivot,data.pixelsPerUnit,0,SpriteMeshType.FullRect);sprite.name=data.spriteName;
  var path=ResourceAssetPath(root,"Sprites",data.sprite,data.spriteName,".asset");AssetDatabase.CreateAsset(sprite,path);var importer=AssetImporter.GetAtPath(path);importer.userData="EngineMigratorSourceId="+data.sprite;importer.SaveAndReimport();ImportedUISprites.Add(data.sprite,sprite);return sprite;
 }
}
