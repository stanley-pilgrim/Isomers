using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Varwin.Public;

namespace Varwin.Editor
{
    [CustomEditor(typeof(VarwinBotTextToSpeech))]
    public class VarwinBotTextToSpeechEditor : UnityEditor.Editor
    {
        private SerializedProperty _lipSyncMesh;
        private SerializedProperty _visemes;
        private SerializedProperty _voiceAudioSource;

        private SkinnedMeshRenderer _oldMesh;

        private static string[] _visemeNames =
        {
            "sil",
            "p",
            "f",
            "th",
            "d",
            "k",
            "ch",
            "s",
            "n",
            "r",
            "a",
            "e",
            "ih",
            "oh",
            "ou"
        };        

        private string[] _blendNames;

        private VarwinBotTextToSpeech _varwinBotTextToSpeech;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            if (_varwinBotTextToSpeech.LipSyncMesh)
            {
                if (_blendNames == null)
                {
                    _blendNames = GetMeshBlendNames();
                }

                if (_blendNames.Length > 0)
                {
                    var repeatedShapes = new List<string>();

                    for (int i = 0; i < _varwinBotTextToSpeech.Visemes.Length - 1; i++)
                    {
                        int viseme = _varwinBotTextToSpeech.Visemes[i];

                        if (viseme == -1)
                        {
                            continue;
                        }

                        for (int j = i + 1; j < _varwinBotTextToSpeech.Visemes.Length; j++)
                        {
                            if (_varwinBotTextToSpeech.Visemes[j] == viseme && !repeatedShapes.Contains(_blendNames[viseme]))
                            {
                                repeatedShapes.Add(_blendNames[viseme]);

                                break;
                            }
                        }
                    }

                    string repeatedShapesString = "";

                    foreach (string shape in repeatedShapes)
                    {
                        repeatedShapesString += shape + "\n";
                    }

                    if (!string.IsNullOrEmpty(repeatedShapesString))
                    {
                        EditorGUILayout.HelpBox($"The following blend shapes are mapped more than once:\n\n{repeatedShapesString}\nLipsync may work incorrectly.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"No blend shapes found for the lipsync mesh \"{_varwinBotTextToSpeech.LipSyncMesh.name}\".", MessageType.Error);
                }
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Speech:", EditorStyles.miniBoldLabel);
            
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_voiceAudioSource);
            EditorGUILayout.PropertyField(_lipSyncMesh);

            if (_varwinBotTextToSpeech.LipSyncMesh)
            {
                if (_varwinBotTextToSpeech.LipSyncMesh != _oldMesh && !Application.isPlaying)
                {
                    _blendNames = GetMeshBlendNames();
                    TryMapVisemes(_blendNames);
                }

                if (_blendNames.Length > 0 && EditorGUILayout.PropertyField(_visemes))
                {
                    EditorGUI.indentLevel++;

                    for (int i = 0; i < _visemeNames.Length; ++i)
                    {
                        BlendNameProperty(_visemes.GetArrayElementAtIndex(i), _visemeNames[i], _blendNames);
                    }

                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
            
            _oldMesh = _varwinBotTextToSpeech.LipSyncMesh;

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _varwinBotTextToSpeech = (VarwinBotTextToSpeech) serializedObject.targetObject;
            
            _lipSyncMesh = serializedObject.FindProperty("LipSyncMesh");
            _visemes = serializedObject.FindProperty("Visemes");
            _voiceAudioSource = serializedObject.FindProperty("VoiceAudioSource");
            
            _oldMesh = _varwinBotTextToSpeech.LipSyncMesh;
        }

        private void BlendNameProperty(SerializedProperty prop, string name, string[] blendNames)
        {
            if (blendNames == null)
            {
                return;
            }

            var values = new int[blendNames.Length + 1];
            var options = new GUIContent[blendNames.Length + 1];
            values[0] = -1;
            options[0] = new GUIContent("None");

            for (int i = 0; i < blendNames.Length; ++i)
            {
                values[i + 1] = i;
                options[i + 1] = new GUIContent(blendNames[i]);
            }

            EditorGUILayout.IntPopup(prop,
                options,
                values,
                new GUIContent(name));
        }

        private string[] GetMeshBlendNames()
        {
            var morphTarget = (VarwinBotTextToSpeech) serializedObject.targetObject;

            if (morphTarget == null || morphTarget.LipSyncMesh == null)
            {
                return null;
            }

            var mesh = morphTarget.LipSyncMesh.sharedMesh;
            string[] blendNames;
            if (mesh)
            {
                var blendshapeCount = mesh.blendShapeCount;
                blendNames = new string[blendshapeCount];

                for (int i = 0; i < mesh.blendShapeCount; ++i)
                {
                    blendNames[i] = mesh.GetBlendShapeName(i);
                }
            }
            else
            {
                blendNames = new string[0];
            }
            return blendNames;
        }

        private void TryMapVisemes(string[] meshBlendNames)
        {
            _varwinBotTextToSpeech.Visemes = Enumerable.Repeat(-1, _varwinBotTextToSpeech.Visemes.Length).ToArray();
            
            for (int i = 0; i < _varwinBotTextToSpeech.Visemes.Length; i++)
            {
                for (int meshBlendIndex = 0; meshBlendIndex < meshBlendNames.Length; meshBlendIndex++)
                {
                    if (Regex.IsMatch(meshBlendNames[meshBlendIndex].ToLower(), $"(^|[^a-z]){_visemeNames[i]}+([^a-z]|$)"))
                    {
                        _varwinBotTextToSpeech.Visemes[i] = meshBlendIndex;
                        break;
                    }
                }
            }            
            
            for (int i = 0; i < _varwinBotTextToSpeech.Visemes.Length; i++)
            {
                for (int meshBlendIndex = 0; meshBlendIndex < meshBlendNames.Length; meshBlendIndex++)
                {
                    if (_varwinBotTextToSpeech.Visemes[i] == -1 && Regex.IsMatch(meshBlendNames[meshBlendIndex].ToLower(), $"v{_visemeNames[i]}+$"))
                    {
                        _varwinBotTextToSpeech.Visemes[i] = meshBlendIndex;
                        break;
                    }
                }
            }            
            
            for (int i = 0; i < _varwinBotTextToSpeech.Visemes.Length; i++)
            {
                for (int meshBlendIndex = 0; meshBlendIndex < meshBlendNames.Length; meshBlendIndex++)
                {
                    if (_varwinBotTextToSpeech.Visemes[i] == -1 && Regex.IsMatch(meshBlendNames[meshBlendIndex].ToLower(), $"{_visemeNames[i]}+$"))
                    {
                        _varwinBotTextToSpeech.Visemes[i] = meshBlendIndex;
                        break;
                    }
                }
            }

        }
        
    }
}
