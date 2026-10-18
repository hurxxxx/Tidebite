using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Tidebite.Editor
{
    public static class IOSBuildSettings
    {
        [PostProcessBuild(100)]
        public static void SetLocalNetworkDescription(BuildTarget target,string path)
        {
            if(target!=BuildTarget.iOS)return;
            string file=Path.Combine(path,"Info.plist");
            var doc=new XmlDocument{XmlResolver=null};doc.Load(file);
            var dict=doc.SelectSingleNode("/plist/dict");
            var key=doc.CreateElement("key");key.InnerText="NSLocalNetworkUsageDescription";
            var value=doc.CreateElement("string");value.InnerText="같은 Wi-Fi의 Tidebite 게임 서버에 접속하기 위해 로컬 네트워크를 사용합니다.";
            var existing=doc.SelectSingleNode("/plist/dict/key[.='NSLocalNetworkUsageDescription']");
            if(existing!=null){var next=existing.NextSibling;dict.RemoveChild(existing);if(next!=null)dict.RemoveChild(next);}
            dict.AppendChild(key);dict.AppendChild(value);
            // XmlDocument otherwise emits an empty internal DTD subset, rejected by Apple's plist reader.
            if(doc.DocumentType!=null)doc.RemoveChild(doc.DocumentType);
            doc.Save(file);
        }
    }
}
