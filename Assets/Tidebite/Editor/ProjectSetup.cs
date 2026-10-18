using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tidebite.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Tidebite/Configure project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Tidebite/Settings");Directory.CreateDirectory("Assets/Tidebite/Scenes");
            const string assetPath="Assets/Tidebite/Settings/MobilePipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if(pipeline==null)
            {
                var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,"Assets/Tidebite/Settings/MobileRenderer.asset");
                pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.name="Tidebite Mobile";
                pipeline.msaaSampleCount=2;pipeline.renderScale=1;pipeline.supportsHDR=false;
                var properties=new SerializedObject(pipeline);
                properties.FindProperty("m_MainLightRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
                properties.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.Disabled;
                properties.FindProperty("m_MainLightShadowsSupported").boolValue=false;
                properties.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
                properties.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline,assetPath);
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i);QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;}
            QualitySettings.SetQualityLevel(2);
            PlayerSettings.companyName="Tidebite Studio";PlayerSettings.productName="Tidebite";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=810;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.tidebite.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.tidebite.game");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString="15.0";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
            const string scenePath="Assets/Tidebite/Scenes/Ocean.unity";
            if(!File.Exists(scenePath)){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,scenePath);}
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            // Runtime art materials resolve these shaders by name; retain them in player builds.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(string name in new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","Tidebite/ReefWater","Tidebite/PaintedSprite","Tidebite/WaterFX","Tidebite/ActionFX","Tidebite/ProfileFish","Tidebite/ProfileMouth"})
            {
                Shader shader=Shader.Find(name);bool exists=false;
                for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)exists=true;
                if(!exists&&shader!=null){int index=shaders.arraySize++;shaders.GetArrayElementAtIndex(index).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();Debug.Log("TIDEBITE CONFIGURED");
        }
        [MenuItem("Tidebite/Build macOS playtest")]
        public static void BuildMac(){Configure();Build("Builds/macOS/Tidebite.app",BuildTarget.StandaloneOSX,false);}
        public static void BuildLinux(){Configure();Build("Builds/LinuxServer/TidebiteServer",BuildTarget.StandaloneLinux64,true);}
        public static void BuildAndroid(){Configure();Build("Builds/Android/Tidebite.apk",BuildTarget.Android,false);}
        public static void BuildIOS(){Configure();Build("Builds/iOS",BuildTarget.iOS,false);}
        private static void Build(string path,BuildTarget target,bool server)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options=new BuildPlayerOptions{scenes=new[]{"Assets/Tidebite/Scenes/Ocean.unity"},locationPathName=path,target=target,
                options=target==BuildTarget.StandaloneOSX?BuildOptions.Development:BuildOptions.None};
            if(target==BuildTarget.StandaloneOSX || target==BuildTarget.StandaloneLinux64)
                options.subtarget=(int)(server?StandaloneBuildSubtarget.Server:StandaloneBuildSubtarget.Player);
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("TIDEBITE BUILD_OK "+path);
        }
    }
}
