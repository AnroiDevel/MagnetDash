using System.IO;
using UnityEditor;

public static class ProjectFoldersCreator
{
    [MenuItem("Tools/Project/Создать структуру скриптов")]
    private static void CreateScriptsFolders()
    {
        CreateFolderIfNotExists("Assets/Scripts/Core/Boot");
        CreateFolderIfNotExists("Assets/Scripts/Core/Systems");
        CreateFolderIfNotExists("Assets/Scripts/Core/Input");
        CreateFolderIfNotExists("Assets/Scripts/Core/Audio");

        CreateFolderIfNotExists("Assets/Scripts/Gameplay/Player");
        CreateFolderIfNotExists("Assets/Scripts/Gameplay/Magnet");
        CreateFolderIfNotExists("Assets/Scripts/Gameplay/Level");
        CreateFolderIfNotExists("Assets/Scripts/Gameplay/Effects");

        CreateFolderIfNotExists("Assets/Scripts/UI/Menus");
        CreateFolderIfNotExists("Assets/Scripts/UI/HUD");
        CreateFolderIfNotExists("Assets/Scripts/UI/Effects");

        CreateFolderIfNotExists("Assets/Scripts/Utilities/Camera");

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

