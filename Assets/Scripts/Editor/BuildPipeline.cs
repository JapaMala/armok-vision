﻿using UnityEditor;
using UnityEngine;
using Ionic.Zip;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using UnityEngine.CloudBuild;
using MaterialStore;

public class BuildFactory
{
    [MenuItem("Mytools/Check HSV values")]
    public static void RGB()
    {
        for(int i = 0; i < 16; i++)
        {
            float h, s, v;
            var color = DfColor.GetColor(i);
            Color.RGBToHSV(color, out h, out s, out v);
            UnityEngine.Debug.Log(string.Format("RGB: {0}, {1}, {2}; HSV: {3}, {4}, {5}", color.r, color.g, color.b, h, s, v));
        }
        UnityEngine.Debug.Log(Color.HSVToRGB(2, 1, 1));
        UnityEngine.Debug.Log(Color.HSVToRGB(1, 2, 1));
        UnityEngine.Debug.Log(Color.HSVToRGB(1, 1, 2));
    }

    [MenuItem("Mytools/Build Release/All")]
    public static void BuildAll()
    {
        BuildRelease(BuildTarget.StandaloneOSX);
        BuildRelease(BuildTarget.StandaloneLinux64);
        BuildRelease(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Mytools/Build Release/Windows")]
    public static void BuildWin()
    {
        BuildRelease(BuildTarget.StandaloneWindows64);
    }
    [MenuItem("Mytools/Build Release/Windows Debug")]
    public static void BuildWinDebug()
    {
        BuildRelease(BuildTarget.StandaloneWindows64, true);
    }

    [MenuItem("Mytools/Build Release/Linux")]
    public static void BuildLinux()
    {
        BuildRelease(BuildTarget.StandaloneLinux64);
    }

    [MenuItem("Mytools/Build Release/macOS")]
    public static void BuildOsx()
    {
        BuildRelease(BuildTarget.StandaloneOSX);
    }

    static void BuildRelease(BuildTarget target, bool isDebug = false)
    {
        MaterialCollector.BuildMaterialCollection();

        string targetString = "";
        string releaseName = "";
        BuildSettings.Instance.build_date = System.DateTime.Now.ToString("yyy-MM-dd");
        EditorUtility.SetDirty(BuildSettings.Instance);
        AssetDatabase.SaveAssets();

        switch (target)
        {
            case BuildTarget.StandaloneOSX:
                releaseName = BuildSettings.Instance.osx_exe;
                targetString = "Mac";
                break;
            case BuildTarget.StandaloneLinux64:
                releaseName = BuildSettings.Instance.linux_exe;
                targetString = "Linux";
                break;
            case BuildTarget.StandaloneWindows:
                releaseName = BuildSettings.Instance.win_exe;
                targetString = "Win";
                break;
            case BuildTarget.StandaloneWindows64:
                releaseName = BuildSettings.Instance.win_exe;
                targetString = "Win x64";
                break;
            default:
                break;
        }

        string path = "Build/" + (isDebug ? BuildSettings.Instance.build_date : BuildSettings.Instance.content_version) + "/" + targetString + (isDebug ? "_debug" : "") + "/" ;

        if (Directory.Exists(path))
            Directory.Delete(path, true);

        string[] levels = new string[] { "Assets/Scenes/Map Mode.unity" };
        EditorUserBuildSettings.SetPlatformSettings("Standalone", "CopyPDBFiles", "false");
        var options = BuildOptions.None;
        if (isDebug)
            options |= BuildOptions.AllowDebugging;
        UnityEngine.Debug.Log(BuildPipeline.BuildPlayer(levels, path + releaseName, target, options));
        CopyExtras(path);

        using (ZipFile zip = new ZipFile())
        {
            zip.AddDirectory(path);
            zip.Save("Build/" + BuildSettings.Instance.title + " " + (isDebug ? BuildSettings.Instance.build_date : BuildSettings.Instance.content_version) + " " + targetString + (isDebug ? "_debug" : "") + ".zip");
        }
    }

    static void CopyExtras(string path)
    {
        path = Path.GetDirectoryName(path) + "/";
        BuildSettings buildSettings = Resources.Load("Build Settings", typeof(BuildSettings)) as BuildSettings;
        FileUtil.ReplaceFile("ReleaseFiles/Changelog.txt", path + "Changelog.txt");
        FileUtil.ReplaceFile("ReleaseFiles/Credits.txt", path + "Credits.txt");
        FileUtil.ReplaceFile("ReleaseFiles/Readme.txt", path + "Readme.txt");
        FileUtil.ReplaceDirectory("ReleaseFiles/Plugins/", path + "Plugins");
        File.WriteAllText(path + "manifest.json", JsonConvert.SerializeObject(buildSettings, Formatting.Indented));
    }

    [MenuItem("Mytools/Build Proto")]
    public static void BuildProto()
    {
        if (!File.Exists("ProtoPath.txt"))
        {
            string tempPath = EditorUtility.OpenFolderPanel("Proto file folder", "", "");
            File.WriteAllText("ProtoPath.txt", tempPath);
        }
        string path = File.ReadAllText("ProtoPath.txt");
        CompileProtoFile(path,
            "RemoteFortressReader.proto", 
            "AdventureControl.proto", 
            "ItemdefInstrument.proto",
            "DwarfControl.proto",
            "ui_sidebar_mode.proto"
            );
        UnityEngine.Debug.Log("Finished compiling protos");
        AssetDatabase.Refresh();
    }

    static void CompileProtoFile(string folder, params string[] protos)
    {
        foreach (var proto in protos)
        {
            File.Copy(Path.Combine(folder, proto), Path.Combine("Assets/RemoteClientLocal/", proto), true);
        }
        Process protogen = new Process();
        protogen.StartInfo.WorkingDirectory = "Assets/RemoteClientLocal/";
        protogen.StartInfo.FileName = Path.Combine(Directory.GetCurrentDirectory(), "ProtoGen/protogen.exe");
        string arguments = "";
        foreach (var proto in protos)
        {
            arguments += "-i:";
            arguments += proto;
            arguments += " ";
        }
        arguments += "-o:protos.cs";
        protogen.StartInfo.Arguments = arguments;

        //redirect output
        protogen.StartInfo.RedirectStandardError = true;
        protogen.StartInfo.RedirectStandardOutput = true;

        protogen.OutputDataReceived += (sender, args) => { if (args.Data != null) UnityEngine.Debug.Log(args.Data); };
        protogen.ErrorDataReceived += (sender, args) => { if (args.Data != null) UnityEngine.Debug.LogError(args.Data); };

        protogen.StartInfo.UseShellExecute = false;
        protogen.StartInfo.CreateNoWindow = true;

        UnityEngine.Debug.Log("Running " + protogen.StartInfo.FileName + " " + protogen.StartInfo.Arguments);

        protogen.Start();

        protogen.BeginOutputReadLine();
        protogen.BeginErrorReadLine();

        protogen.WaitForExit();
    }

    public static void PreBuild(BuildManifestObject manifest)
    {
        MaterialCollector.BuildMaterialCollection();
        RenderTexture.active = null; //Attempt at cloud build error fixing.
    }
}
