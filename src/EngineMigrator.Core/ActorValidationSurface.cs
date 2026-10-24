namespace EngineMigrator.Core;
internal static class ActorValidationSurface
{
    internal const string Source="""
namespace UnityEngine {
 public class SerializeField:System.Attribute {}
 public class Object {
  public static implicit operator bool(Object value)=>!System.Object.ReferenceEquals(value,null);
  public static bool operator !(Object value)=>System.Object.ReferenceEquals(value,null);
  public static bool operator ==(Object a,Object b)=>System.Object.ReferenceEquals(a,b);
  public static bool operator !=(Object a,Object b)=>!System.Object.ReferenceEquals(a,b);
  public override bool Equals(object value)=>System.Object.ReferenceEquals(this,value);
  public override int GetHashCode()=>base.GetHashCode();
  public string name;
  public static GameObject Instantiate(GameObject original)=>original;
  public static GameObject Instantiate(GameObject original,Transform parent)=>original;
  public static GameObject Instantiate(GameObject original,Transform parent,bool worldPositionStays)=>original;
  public static GameObject Instantiate(GameObject original,Vector3 position,Quaternion rotation)=>original;
  public static GameObject Instantiate(GameObject original,Vector3 position,Quaternion rotation,Transform parent)=>original;
  public static void Destroy(GameObject value){}
  public static void Destroy(MonoBehaviour value){}
  public static void Destroy(Canvas value){}
  public static void Destroy(UnityEngine.UI.Button value){}
 }
 public class MonoBehaviour:Object {public bool enabled=true;public Transform transform;public GameObject gameObject;public T GetComponent<T>() where T:Object=>default;}
 public class GameObject:Object {public GameObject(){}public GameObject(string name){}public Transform transform;public bool activeSelf,activeInHierarchy;public void SetActive(bool value){}public T AddComponent<T>() where T:Object=>default;public T GetComponent<T>() where T:Object=>default;}
 public class Canvas:Object {public bool enabled=true;public GameObject gameObject;public Transform transform;}
 public class Transform:Object {public Vector3 position,localPosition,localScale;public Quaternion rotation,localRotation;public Transform parent;public GameObject gameObject;public void SetParent(Transform parent){}public void SetParent(Transform parent,bool worldPositionStays){}}
 public struct Quaternion {public float x,y,z,w;public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}public static Quaternion identity;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero,one,right,up,forward;public static Vector3 operator +(Vector3 a,Vector3 b)=>a;public static Vector3 operator -(Vector3 a,Vector3 b)=>a;public static Vector3 operator -(Vector3 a)=>a;public static Vector3 operator *(Vector3 a,float b)=>a;public static Vector3 operator *(float a,Vector3 b)=>b;public static Vector3 operator /(Vector3 a,float b)=>a;}
 public static class Time {public static float deltaTime;public static float fixedDeltaTime;public static float timeScale;}
}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void LoadScene(string name){}public static void LoadScene(int index){}}}
namespace UnityEngine.UI {public class Button:UnityEngine.Object {public bool enabled=true,interactable=true;public UnityEngine.GameObject gameObject;public UnityEngine.Transform transform;}}
""";
}
