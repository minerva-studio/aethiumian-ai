using Minerva.Module;
using Minerva.Module.Editor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomPropertyDrawer(typeof(BehaviourTree))]
    public class BehaviourTreeEditor : PropertyDrawer
    {
        public const bool debug = false;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            BehaviourTree bt = (BehaviourTree)property.GetValue();
            int pCount = 0;
            pCount++;//header
            pCount++;//edit
            pCount++;//inspector
            pCount++;//enable
            pCount++;//breaks
            if (bt.IsRunning || debug)
            {
                pCount++;//paused
                if (bt.MainStack != null) pCount++;//sleep

                pCount++;//last stage
                pCount++;//current stage
                pCount++;//current stage time
                pCount++;//stack info
                if (bt.MainStack != null)
                {
                    pCount += bt.MainStack.Count;
                }
                pCount++;//ServiceStacks info
                if (bt.ServiceStacks != null)
                {
                    pCount += bt.ServiceStacks.Count;
                    if (bt.ServiceStacks.Count != 0)
                    {
                        pCount += bt.ServiceStacks.SelectMany(c => c.Value.Nodes).Count();
                    }
                }
                pCount++;// pause/continue
            }
            else
            {
                pCount += 3;
            }
            return pCount * EditorGUIUtility.singleLineHeight;


        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIContent label2;
            BehaviourTree bt = (BehaviourTree)property.GetValue();
            Rect singleRect = position;
            singleRect.height = EditorGUIUtility.singleLineHeight;

            //head
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(singleRect, label);
            EditorGUI.indentLevel++;

            //enabled
            label = new GUIContent { text = nameof(bt.IsRunning).ToTitleCase() };
            singleRect.y += EditorGUIUtility.singleLineHeight;
            var prev = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.PropertyField(singleRect, property.FindPropertyRelative("isRunning"), label);
            GUI.enabled = prev;

            if (bt.IsRunning || debug)
            {
                //breaks
                label = new GUIContent { text = "Set Break Points" };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(singleRect, property.FindPropertyRelative("pauseAfterSingleExecution"), label);

                //paused
                label = new GUIContent { text = nameof(bt.IsPaused).ToTitleCase() };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                GUI.enabled = false;
                EditorGUI.Toggle(singleRect, label, bt.IsPaused);
                GUI.enabled = true;
                //sleep
                if (bt.MainStack != null)
                {
                    singleRect.y += EditorGUIUtility.singleLineHeight;
                    GUI.enabled = false;
                    EditorGUI.LabelField(singleRect, "State", bt.MainStack.State.ToString());
                    GUI.enabled = true;
                }

                //Last stage
                label = new GUIContent { text = nameof(bt.LastStage).ToTitleCase() };
                label2 = new GUIContent { text = bt.LastStage?.name ?? "None" };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, label, label2);

                //current stage
                label = new GUIContent { text = nameof(bt.CurrentStage).ToTitleCase() };
                label2 = new GUIContent { text = bt.CurrentStage?.name ?? "None" };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, label, label2);

                //current stage time
                label = new GUIContent { text = nameof(bt.CurrentStageDuration).ToTitleCase() };
                label2 = new GUIContent { text = bt.CurrentStageDuration.ToString() };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, label, label2);

                //stack
                singleRect = DrawMainStack(bt, singleRect);

                //service stack
                singleRect = DrawServiceStack(bt, singleRect);

            }
            else
            {
                //head 
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, "");
                //head 
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, "Behaviour Tree not running");
                //head 
                singleRect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(singleRect, "");
            }
            DrawButtons(property, bt, singleRect);

            EditorGUI.EndProperty();
            EditorGUI.indentLevel--; 
        }

        private Rect DrawMainStack(BehaviourTree bt, Rect singleRect)
        {
            if (bt.MainStack != null)
            {
                var progressStack = bt.MainStack.Nodes;
                string name = "Progress Stack";
                singleRect = DrawStack(singleRect, progressStack, name);
            }

            return singleRect;
        }

        private Rect DrawServiceStack(BehaviourTree bt, Rect singleRect)
        {
            var label = new GUIContent { text = "Service" };
            var label2 = new GUIContent { text = bt.ServiceStacks.Count.ToString() };

            singleRect.y += EditorGUIUtility.singleLineHeight;
            if (bt.ServiceStacks != null)
            {
                EditorGUI.LabelField(singleRect, label, label2);
                EditorGUI.indentLevel++;
                foreach (var item in bt.ServiceStacks)
                {
                    var progressStack = item.Value.Nodes;
                    var name = item.Key.name ?? "Null";
                    singleRect = DrawStack(singleRect, progressStack, name);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                label2 = new GUIContent { text = "0" };
                EditorGUI.LabelField(singleRect, label, label2);
            }

            return singleRect;
        }

        private void DrawButtons(SerializedProperty property, BehaviourTree bt, Rect singleRect)
        {
            //button
            GUIContent label;

            label = new GUIContent { text = "Open Editor" };
            singleRect.y += EditorGUIUtility.singleLineHeight;
            if (GUI.Button(singleRect, label))
            {
                AI ai;
                var window = AIEditor.ShowWindow();
                window.Load(property.serializedObject.FindProperty(nameof(ai.data)).objectReferenceValue as BehaviourTreeData);
            }

            label = new GUIContent { text = "Open Inspector" };
            singleRect.y += EditorGUIUtility.singleLineHeight;
            if (GUI.Button(singleRect, label))
            {
                var window = AIInspector.ShowWindow();
                window.Load(property.serializedObject.context as AI);
            }


            if (!bt.IsRunning)
            {
                return;
            }

            if (bt.IsPaused)
            {
                //button
                label = new GUIContent { text = "Continue" };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                if (GUI.Button(singleRect, label))
                {
                    bt.Resume();
                }
            }
            else
            {
                //button
                label = new GUIContent { text = "Pause" };
                singleRect.y += EditorGUIUtility.singleLineHeight;
                if (GUI.Button(singleRect, label))
                {
                    bt.Pause();
                }
            }
        }

        private Rect DrawStack(Rect singleRect, List<TreeNode> progressStack, string name)
        {
            singleRect.y += EditorGUIUtility.singleLineHeight;
            GUIContent label = new GUIContent { text = name.ToTitleCase() };
            GUIContent label2;
            if (progressStack != null)
            {
                label2 = new GUIContent { text = progressStack.Count.ToString() };
                EditorGUI.LabelField(singleRect, label, label2);

                EditorGUI.indentLevel++;
                foreach (var item in ((IEnumerable<TreeNode>)progressStack).Reverse())
                {
                    label = new GUIContent { text = item.name.ToTitleCase() };
                    singleRect.y += EditorGUIUtility.singleLineHeight;
                    EditorGUI.LabelField(singleRect, label);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                label2 = new GUIContent { text = "0" };
                EditorGUI.LabelField(singleRect, label, label2);
            }
            return singleRect;
        }
    }
}