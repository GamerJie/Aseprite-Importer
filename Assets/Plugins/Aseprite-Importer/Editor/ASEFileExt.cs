
using System.IO;
using System.Linq;
using MetaSprite;
using MetaSprite.Internal;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DefaultAsset))]
public class FileExt: Editor
{
    private ImportSettings _settings;

    private void OnEnable()
    {
        _settings = CreateInstance<ImportSettings>();
    }

    public override void OnInspectorGUI ()
    {
        string path = AssetDatabase.GetAssetPath(target);

        _settings.baseName = target.name;
        _settings.spriteTarget = target.name;
        var res = path.Split('/');
        var resPath = path.Replace(res[res.Length - 1], "");
        _settings.atlasOutputDirectory = resPath;
        _settings.clipOutputDirectory = resPath;
        _settings.animControllerOutputPath = resPath;
 
        GUI.enabled = true;
        if(path.EndsWith(".ase"))
        {
            AseShow();
        }
    }

    private void AseShow()
    {
        EditorGUI.BeginChangeCheck();

        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Options");
        }

        _settings.baseName = EditorGUILayout.TextField("Base Name", _settings.baseName);
        _settings.spriteTarget = EditorGUILayout.TextField("Target Child Object", _settings.spriteTarget);
        EditorGUILayout.Space();

        _settings.ppu = EditorGUILayout.IntField("Pixel Per Unit", _settings.ppu);
        _settings.alignment = (SpriteAlignment) EditorGUILayout.EnumPopup("Default Align", _settings.alignment);
        if (_settings.alignment == SpriteAlignment.Custom) {
            _settings.customPivot = EditorGUILayout.Vector2Field("Custom Pivot", _settings.customPivot);
        }

        _settings.densePacked = EditorGUILayout.Toggle("Dense Pack", _settings.densePacked);
        _settings.border = EditorGUILayout.IntField("Border", _settings.border);

        EditorGUILayout.Space();
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Output");
        }
        
        _settings.atlasOutputDirectory = PathSelection("Atlas Directory", _settings.atlasOutputDirectory);
        _settings.clipOutputDirectory = PathSelection("Anim Clip Directory", _settings.clipOutputDirectory);

        _settings.controllerPolicy = (AnimControllerOutputPolicy) EditorGUILayout.EnumPopup("Anim Controller Policy", _settings.controllerPolicy);
        if (_settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride) {
            _settings.animControllerOutputPath = PathSelection("Anim Controller Directory", _settings.animControllerOutputPath);
        }

        if (EditorGUI.EndChangeCheck()) {
            EditorUtility.SetDirty(_settings);
        }

        if (GUILayout.Button("Gen"))
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            // ASEImporter.Import(path, _settings);

            var context = new ImportContext {
                settings = _settings,
                fileDirectory = Path.GetDirectoryName(path),
                fileName = Path.GetFileName(path),
                fileNameNoExt = Path.GetFileNameWithoutExtension(path)
            };
            
            context.file = ASEParser.Parse(File.ReadAllBytes(path)); 
            context.atlasPath = Path.Combine(_settings.atlasOutputDirectory, context.fileNameNoExt + ".png");
            context.animClipDirectory = _settings.clipOutputDirectory;
            
            if (_settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride)
                context.animControllerPath = _settings.animControllerOutputPath + "/" + _settings.baseName + ".controller";
            
            Directory.CreateDirectory(_settings.atlasOutputDirectory);
            Directory.CreateDirectory(_settings.clipOutputDirectory);
            if (context.animControllerPath != null)
                Directory.CreateDirectory(_settings.animControllerOutputPath);
            
            context.generatedSprites = AtlasGenerator.GenerateAtlas(context, 
                context.file.layers.Where(it => it.type == LayerType.Content).ToList(),
                context.atlasPath);
            
            CreateClips(context);
        }
    }

    private void CreateClips(ImportContext context)
    {
        var file = context.file;

        foreach (var frameTag in file.frameTags)
        {
            AnimationClip clip = new AnimationClip();
            AnimationUtility.SetAnimationType(clip, ModelImporterAnimationType.Generic);
            EditorCurveBinding curveBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var frmaes = file.frames.GetRange(frameTag.@from, frameTag.to + 1 - frameTag.@from);
            var spList = context.generatedSprites.GetRange(frameTag.@from, frmaes.Count);

            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[frmaes.Count];
            int time = 0;
            for (int i = 0; i < frmaes.Count; i++)
            {
                keyFrames[i] = new ObjectReferenceKeyframe
                {
                    time = time * 1e-3f,
                    value = spList[i]
                };
                Debug.Log(spList[i].name);
                
                time += frmaes[i].duration;
            }
            
            AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyFrames);
            AssetDatabase.CreateAsset(clip, context.animClipDirectory + context.settings.baseName + "_" + frameTag.name + ".anim");
        
            AssetDatabase.SaveAssets();
        }
    }
    
    private string PathSelection(string id, string path) {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(id);
        path = EditorGUILayout.TextField(path);
        if (GUILayout.Button("...", GUILayout.Width(30))) {
            path = GetAssetPath(EditorUtility.OpenFolderPanel("Select path", path, ""));
        }

        EditorGUILayout.EndHorizontal();
        return path;
    }

    private static string GetAssetPath(string path) {
        if (path == null) {
            return null;
        }

        var projectPath = Application.dataPath;
        projectPath = projectPath.Substring(0, projectPath.Length - "/Assets".Length);
        path = Remove(path, projectPath);

        if (path.StartsWith("\\") || path.StartsWith("/")) {
            path = path.Remove(0, 1);
        }

        if (!path.StartsWith("Assets") && !path.StartsWith("/Assets")) {
            path = Path.Combine("Assets", path);
        }

        path.Replace('\\', '/');

        return path;
    }

    static string Remove(string s, string exactExpression) {
        return s.Replace(exactExpression, "");
    }
}