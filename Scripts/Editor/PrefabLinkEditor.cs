﻿using UnityEngine;
using UnityEditor;
using TP.ExtensionMethods;
using System;
using System.Linq;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;

namespace TP.Greenfab
{
    [CustomEditor(typeof(PrefabLink)), ExecuteInEditMode, CanEditMultipleObjects, InitializeOnLoad, Serializable]
    public class PrefabLinkEditor : Editor
    {
        [SerializeField] public string message = "";
        [SerializeField] public MessageType messageType = MessageType.None;
        [SerializeField] public float messageDuration = 1;
        [SerializeField] public List<PrefabLink> prefabLinks;
        [SerializeField] public bool triggerRevert = false;
        [SerializeField] public bool triggerRevertHierarchy = false;
        [SerializeField] public bool triggerApply = false;

        public Texture2D prefabLinkIcon;

        static PrefabLinkEditor()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
        }

        private void OnEnable()
        {
            MovePrefabLinksToTop();
        }

        void Reset()
        {
            PrefabLink[] prefabLinksTemp = Array.ConvertAll(targets, item => (PrefabLink)item);

            foreach (PrefabLink prefabLink in prefabLinksTemp)
            {
                UnityEditorInternal.ComponentUtility.MoveComponentUp(prefabLink.GetComponent<Component>());

                //Stops unity from breaking prefab reference when adding prefab to scene.
                if (prefabLink.prefab && !prefabLink.prefab.IsPrefab() && prefabLink.prefab == prefabLink.gameObject)
                {
                    GameObject prefab = PrefabUtility.GetPrefabParent(prefabLink.gameObject) as GameObject;
                    PrefabUtility.DisconnectPrefabInstance(prefabLink.gameObject);

                    prefabLink.prefab = prefab;
                }

                //When adding prefab link to a prefab automatically add reference to self.
                if (!prefabLink.prefab && prefabLink.gameObject.IsPrefab())
                {
                    prefabLink.prefab = prefabLink.gameObject;
                }
            }
        }

        public override void OnInspectorGUI()
        {            
            Object obj = Selection.activeObject;
            if (obj != null)
            {
                prefabLinkIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Packages/TP/Prefabulous/Textures/Prefabulous Icon Big.png");
                IconManager.SetIcon(obj as GameObject, prefabLinkIcon);
            }

            MovePrefabLinksToTop();

            float buttonWidth = (EditorGUIUtility.currentViewWidth / 3) - 20;
            float buttonHeight = EditorGUIUtility.singleLineHeight;
            
            prefabLinks = Array.ConvertAll(targets, item => (PrefabLink)item).ToList();
            PrefabLink firstPrefabLink = prefabLinks[0];
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", GUILayout.Width(40));
            firstPrefabLink.prefab = EditorGUILayout.ObjectField(firstPrefabLink.prefab, typeof(GameObject), GUILayout.ExpandWidth(true)) as GameObject;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            bool prefabFileSelected = false;

            bool canRevert = true;

            foreach (PrefabLink prefabLink in prefabLinks)
            {
                if (prefabLink.gameObject.IsPrefab())
                {
                    prefabFileSelected = true;
                }

                if (prefabLink.prefab.GetInstanceID() == prefabLink.gameObject.GetInstanceID())
                {
                    canRevert = false;
                }
            }

            GUI.enabled = canRevert;
            
            if (GUILayout.Button("Revert", GUILayout.Width(buttonWidth)))
            {
                triggerRevert = true;
                EditorApplication.update += Update;
            }
            
            GUI.enabled = true;

            if (GUILayout.Button("Revert All", GUILayout.Width(buttonWidth)))
            {
                triggerRevertHierarchy = true;
                EditorApplication.update += Update;
            }
            
            GUI.enabled = canRevert;

            if (GUILayout.Button("Apply", GUILayout.Width(buttonWidth)))
            {
                triggerApply = true;
                EditorApplication.update += Update;
            }

            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;

            foreach (PrefabLink prefabLink in prefabLinks)
            {
                float startTime = prefabLink.startTime;
                bool revertSuccessful = prefabLink.revertSuccessful;
                float messageDisplayTime = (float)EditorApplication.timeSinceStartup - startTime;

                if (messageDisplayTime < messageDuration && messageDisplayTime > 0)
                {
                    if (!revertSuccessful)
                    {
                        message = prefabLink.name + " - Reverting may have ran into issues. See console output.";
                        messageType = MessageType.Warning;
                    }
                    else
                    {
                        message = prefabLink.name + " - Reverting succesful.";
                        messageType = MessageType.Info;
                    }

                    EditorGUILayout.HelpBox(message, messageType);
                }

            }

            //DEBUGING FIELDS
            
            //EditorGUILayout.TextArea("timeSinceStartup: " + EditorApplication.timeSinceStartup);
            //EditorGUILayout.TextArea("messageStartTime: " + firstPrefabLink.startTime);
            //EditorGUILayout.TextArea("hierarchyCount: " + firstPrefabLink.transform.hierarchyCount);
            //EditorGUILayout.TextArea("hierarchyCapacity: " + firstPrefabLink.transform.hierarchyCapacity);
            //EditorGUILayout.TextArea("childCount: " + firstPrefabLink.transform.childCount);
            //EditorGUILayout.TextArea("parentDepth: " + firstPrefabLink.transform.ParentDepth());

        }

        private void Update()
        {
            EditorApplication.update -= Update;
            
            if (triggerRevert)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    Undo.RegisterFullObjectHierarchyUndo(prefabLink, "Prefab Link");
                    prefabLink.startTime = (float)EditorApplication.timeSinceStartup;
                    prefabLink.Revert(false, true);
                    EditorUtility.SetDirty(prefabLink);
                }
            }

            if (triggerRevertHierarchy)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    Undo.RegisterFullObjectHierarchyUndo(prefabLink, "Prefab Link");
                    prefabLink.startTime = (float)EditorApplication.timeSinceStartup;
                    prefabLink.Revert(true, true);
                    EditorUtility.SetDirty(prefabLink);
                }
            }

            if (triggerApply)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    //Uhis for applying prefabse is broken 
                    //https://issuetracker.unity3d.com/issues/reverting-changes-on-applied-prefab-crashes-unity
                    //if (prefabLink.prefab != null)
                    //{
                    //    Undo.RegisterFullObjectHierarchyUndo(prefabLink.prefab, "Prefab Link - Prefab");
                    //}
                    GameObject newPrefab = PrefabUtility.ReplacePrefab(prefabLink.gameObject, prefabLink.prefab);
                    prefabLink.prefab = newPrefab;
                    //EditorUtility.SetDirty(prefabLink.prefab); 
                }
            }

            Undo.FlushUndoRecordObjects();

            triggerRevert = false;
            triggerRevertHierarchy = false;
            triggerApply = false;
        }

        private void MovePrefabLinksToTop()
        {
            PrefabLink[] prefabLinksTemp = Array.ConvertAll(targets, item => (PrefabLink)item);

            foreach (PrefabLink prefabLink in prefabLinksTemp)
            {
                UnityEditorInternal.ComponentUtility.MoveComponentUp(prefabLink);
            }
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            Color prefabLinkColor = new Color32(0, 125, 0, 255);
            Color background = Color.blue;
            Color backgroundColor = new Color32(194, 194, 194, 255);

            UnityEngine.Object obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj != null)
            {
                if (obj is GameObject)
                {
                    GameObject gameObject = obj as GameObject;

                    if (gameObject.gameObject.GetComponent<PrefabLink>() != null)
                    {
                        if (Selection.instanceIDs.Contains(instanceID))
                        {
                            prefabLinkColor = new Color32(100, 200, 100, 255);
                            backgroundColor = new Color(.24f, .48f, .90f);
                        }

                        if (!gameObject.activeInHierarchy)
                        {
                            prefabLinkColor = new Color32(100, 150, 100, 255);
                        }

                        Rect offsetRect = new Rect(selectionRect.position + new Vector2(0, 2), selectionRect.size);
                        EditorGUI.DrawRect(selectionRect, backgroundColor);
                        EditorGUI.LabelField(offsetRect, obj.name, new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = prefabLinkColor },
                            fontStyle = FontStyle.Normal
                        });
                    }
                }
            }
        }
    }
}