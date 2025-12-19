using System.IO;
using UnityEditor;

public static class ProjectFoldersCreator
{
    private static readonly string[] ScriptFolders =
    {
        "Assets/Scripts/AudioInGame",
        "Assets/Scripts/Core/Audio",
        "Assets/Scripts/Core/Boot",
        "Assets/Scripts/Core/Input",
        "Assets/Scripts/Core/Systems",
        "Assets/Scripts/Economy/Meta",
        "Assets/Scripts/Economy/Shop",
        "Assets/Scripts/Gameplay/Effects",
        "Assets/Scripts/Gameplay/Level",
        "Assets/Scripts/Gameplay/Magnet",
        "Assets/Scripts/Gameplay/Player",
        "Assets/Scripts/Services",
        "Assets/Scripts/Services/Ads",
        "Assets/Scripts/Services/AudioSysten",
        "Assets/Scripts/Services/Levels",
        "Assets/Scripts/Tutorials",
        "Assets/Scripts/UI/Effects",
        "Assets/Scripts/UI/HUD",
        "Assets/Scripts/UI/Menus",
        "Assets/Scripts/Utilities/Camera",
        "Assets/Scripts/Utilities/JsonGenerator"
    };

    [MenuItem("Tools/Project/Create Script Folders")]
    private static void CreateScriptsFolders()
    {
        foreach(var folder in ScriptFolders)
        {
            CreateFolderIfNotExists(folder);
        }

        AssetDatabase.Refresh();
    }

    private static void CreateFolderIfNotExists(string folderPath)
    {
        if(AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var newFolderName = Path.GetFileName(folderPath);

        if(!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            CreateFolderIfNotExists(parent);
        }

        if(!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(newFolderName))
        {
            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}
