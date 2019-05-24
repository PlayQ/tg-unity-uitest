﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using EditorUITools;
using Tests.Nodes;
using UnityEditor;
using UnityEngine;

namespace PlayQ.UITestTools
{ 
    [Serializable]
    public class Filter
    {
        public Filter(List<MethodNode> allMethodNodes)
        {
            this.allMethodNodes = allMethodNodes;
        }
        
        private List<MethodNode> allMethodNodes;
        public void UpdateFilter(string newFilter)
        {
            if (!newFilter.Equals(filter, StringComparison.Ordinal))
            {
                filter = newFilter;
                if (string.IsNullOrEmpty(filter))
                {
                    foreach (var method in allMethodNodes)
                    {
                        method.SetHided(false);
                        method.View.SetOpenFiltered(false);
                        var parent = method.Parent; 
                        while (parent != null && parent.View.IsOpenFiltered.HasValue)
                        {
                            parent.View.SetOpenFiltered(null);
                            parent = parent.Parent;
                        }
                    }
                }
                else
                {
                    foreach (var method in allMethodNodes)
                    {
                        if (method.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) != -1)
                        {   
                            method.SetHided(false);
                            method.View.SetOpenFiltered(true);
                            var parent = method.Parent; 
                            while (parent != null && (!parent.View.IsOpenFiltered.HasValue ||
                                   parent.View.IsOpenFiltered.HasValue && !parent.View.IsOpenFiltered.Value))
                            {
                                parent.View.SetOpenFiltered(true);
                                parent = parent.Parent;
                            }
                        }
                        else
                        {    
                            method.SetHided(true);
                        }
                    }
                }
            }
        }

        [SerializeField] private string filter = string.Empty;
        public string FilterString
        {
            get { return filter; }
        }
    }
    public class RuntimeTestRunnerWindow : EditorWindow
    {
        
        protected static Texture2D successIcon;
        protected static Texture2D failIcon;
        protected static Texture2D ignoreIcon;
        protected static Texture2D defaultIcon;

        private bool previousTestsIsRunning;

        [SerializeField] private Filter filter;

        private int runTestsGivenAmountOfTimes = -1;
        private int currentTimescale = -1;
        private const int PROGRESS_BAR_HEIGHT = 22;
        private SelectedNode selectedNode;

        [SerializeField]
        private TwoSectionWithSliderDrawer twoSectionDrawer;
        [SerializeField]
        private UITestToolWindowFooter footer;
      
        [MenuItem("Window/UI Test Tools/Runtime Test Runner")]
        private static void ShowWindow()
        {
            GetWindow(typeof(RuntimeTestRunnerWindow), false, "Test Runner").Show();
        }

        private GUIStyle offsetStyle;
        private ClassNode rootNode; 
        public List<MethodNode> AllMethodNodes { get; private set; }

        private void Init()
        {
            if (rootNode == null)
            {
                rootNode = PlayModeTestRunner.TestsRootNode;
                if (rootNode == null)
                {
                    return;
                }
                currentPlayingTest = PlayModeTestRunner.CurrentPlayingMethodNode;
                
                rootNode.StateUpdated += CalculateAllTestsResults;
                AllMethodNodes = rootNode.GetChildrenOfType<MethodNode>();

                string oldFilterValue = String.Empty;
                if (filter != null)
                {
                    oldFilterValue = filter.FilterString;
                }
                filter = new Filter(AllMethodNodes);
                selectedNode = new SelectedNode(rootNode);
                
                InitNodeView();
                filter.UpdateFilter(oldFilterValue);
            }
        }

        private void OnEnable()
        {
            footer = new UITestToolWindowFooter();
            if (twoSectionDrawer == null)
            {
                twoSectionDrawer = new TwoSectionWithSliderDrawer(
                    50,
                    50,
                    int.MaxValue
                );
            }
            LoadImgs();

            PlayModeTestRunner.OnMethodStateUpdated += Repaint;
            TestStep.OnTestStepUpdated += Repaint;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            PlayModeTestRunner.OnMethodStateUpdated -= Repaint;
            TestStep.OnTestStepUpdated -= Repaint;
            EditorApplication.update -= OnEditorUpdate;
        }


        public PlayModeTestRunner.PlayingMethodNode currentPlayingTest;
        
        private void OnGUI()
        {   
            Init();
            
            if (GUI.GetNameOfFocusedControl() != string.Empty)
            {
                selectedNode.UpdateSelectedNode(null);
            }

            
            DrawHeader((int) position.width);
            
            if (rootNode == null)
            {
                GUILayout.Label("No serialized tests found after compilation");
                GUILayout.Label("You can adjust tests updating in Advanced Options");
            }
            else
            {
                DrawStatistics();
                DrawFilter();        
                DrawClasses();
                DrawFooter();
            }
        }

        private void DrawFilter()
        {
            GUI.SetNextControlName("Filter");
            var newFilter = EditorGUILayout.TextField("Filter:", filter.FilterString);
            filter.UpdateFilter(newFilter);
        }
        
        delegate bool IsSerializedAssetDirty(int instanceID);
        private IsSerializedAssetDirty CheckIsAssetDirty;
        private int instanceID;
        
        private void CheckAssetIsDirty()
        {
            if (CheckIsAssetDirty == null)
            {
                var methodInfo = typeof(EditorUtility).GetMethod("IsDirty", 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                var customDelegate = Delegate.CreateDelegate(
                    typeof(IsSerializedAssetDirty), methodInfo, false);
                CheckIsAssetDirty = (IsSerializedAssetDirty) customDelegate;
                instanceID = PlayModeTestRunner.SerializedTests.GetInstanceID();
            }

            
            isDirty = CheckIsAssetDirty(instanceID);

            if (!PlayModeTestRunner.IsRunning && previousIsDirty != isDirty && !isDirty)
            {
                PlayModeTestRunner.SaveTestsData();
                AssetDatabase.SaveAssets();
            }
            previousIsDirty = isDirty;
        }

        private void DrawFooter()
        {
            footer.Draw(position, isDirty);
            
            if (PlayModeTestRunner.IsRunning)
            {
                float positionY = position.height - (PROGRESS_BAR_HEIGHT + EditorUIConstants.LAYOUT_PADDING);

                if (isDirty)
                {
                    positionY -= UITestToolWindowFooter.HEIGHT + EditorUIConstants.LAYOUT_PADDING;
                }

                Rect r = new Rect(
                    new Vector2(0, positionY),
                    new Vector2(position.width, PROGRESS_BAR_HEIGHT));

                int maxSteps = currentPlayingTest.Node.Step;
                
                var currentStep = TestStep.GetCurrentStep(maxSteps);
                if (!PlayModeTestRunner.IsRunning || currentStep == null)
                {
                    GUI.enabled = false;
                    EditorGUI.ProgressBar(r, 0, "");
                    GUI.enabled = true;
                }
                else
                {
                    EditorGUI.ProgressBar(r, currentStep.Progress, currentStep.ToString());
                }
            }
        }

        
        private void InitNodeView()
        {
            var rootView = new RootView(rootNode, selectedNode);
            rootNode.SetView(rootView);
            
            foreach (var classNode in rootNode.GetChildrenOfType<ClassNode>())
            {
                var view = new ClassNodeView(classNode, selectedNode, currentPlayingTest);
                classNode.SetView(view);
            }

            foreach (var methodNode in AllMethodNodes)
            {
                var view = new MethodNodeView(methodNode, selectedNode, currentPlayingTest);
                methodNode.SetView(view);
            }   
        }

        private bool isDirty;
        private bool previousIsDirty;
        
        private void DrawClasses()
        {
            float footer = 0;

            footer += UITestToolWindowFooter.HEIGHT + EditorUIConstants.LAYOUT_PADDING;

            if (PlayModeTestRunner.IsRunning)
            {
                footer += PROGRESS_BAR_HEIGHT + EditorUIConstants.LAYOUT_PADDING;
            }

            bool needToRepaint = twoSectionDrawer.Draw(
                position,
                GUILayoutUtility.GetLastRect().max.y,
                footer, EditorStyles.helpBox,
                () =>
                {
                    var isDirty = rootNode.View.Draw();
                    if (isDirty)
                    {
                        EditorUtility.SetDirty(PlayModeTestRunner.SerializedTests);
                    }
                    
                },
                GUIStyle.none,
                DrawLowerSection);
            CheckAssetIsDirty();
            if (needToRepaint)
            {
                Repaint();
            }
        }

        private void DrawLowerSection()
        {
            if (selectedNode.Node is MethodNode)
            {
                var methodNode = (MethodNode) selectedNode.Node;
                List<string> reverseLogs = 
                    new List<string>(methodNode.Logs);
                reverseLogs.Reverse();

                string totalLog = String.Join("\n", reverseLogs.ToArray());
                totalLog = totalLog.Substring(0, Math.Min(totalLog.Length, 15000 - 4));

                if (totalLog.Length == (15000 - 4))
                    totalLog = string.Concat(totalLog, "\n...");

                GUILayout.TextArea(totalLog);
            }
        }

        private float GetContentLenght(GUIContent content)
        {
            GUIStyle style = GUI.skin.label;
            Vector2 size = style.CalcSize(content);
            return size.x;
        }

        private void LoadImgs()
        {
            if (successIcon == null)
            {
                successIcon = Resources.Load<Texture2D>("passed");
                failIcon = Resources.Load<Texture2D>("failed");
                ignoreIcon = Resources.Load<Texture2D>("ignored");
                defaultIcon = Resources.Load<Texture2D>("normal");
            }
        }
        
        private int allPassedTests;
        private int allIgnoredTests;
        private int allFailedTests;
        private int totalTests;
        
        [SerializeField]
        private bool ShowAdvancedOptions;
        public DefaultAsset EditorTestResourcesFolder;
        public DefaultAsset BuildTestResourcesFolder;
        public bool UpdateTestsOnEveryCompilation;
        private int editorUpdateEnabledForFrames; 
        

        private void CalculateAllTestsResults()
        { 
            allPassedTests = 0;
            allIgnoredTests = 0;
            allFailedTests = 0;
            totalTests = AllMethodNodes.Count;
            
            foreach (var testMethod in AllMethodNodes)
            {
                switch (testMethod.State)
                {
                    case TestState.Passed:
                        allPassedTests++;
                        break;
                    case TestState.Ignored:
                        allIgnoredTests++;
                        break;
                    case TestState.Failed:
                        allFailedTests++;
                        break;
                }
            }
        }

        private void DrawStatistics()
        {   
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Passed:", successIcon), GUILayout.Width(100f));
            EditorGUILayout.LabelField(allPassedTests.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Failed:", failIcon), GUILayout.Width(100f));
            EditorGUILayout.LabelField(allFailedTests.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Ignored:", ignoreIcon), GUILayout.Width(100f));
            EditorGUILayout.LabelField(allIgnoredTests.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Total:", defaultIcon), GUILayout.Width(100f));
            EditorGUILayout.LabelField(totalTests.ToString());
            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }


        private void DrawHeader(int width)
        {
            GUI.enabled = !Application.isPlaying && rootNode != null;
            
            const int buttonOffset = 3;

            width -= 10;
            
            GUILayout.Space(5f);

            EditorGUILayout.BeginHorizontal();

            int buttonSize = width/4 - buttonOffset;

            if (GUILayout.Button("Select all", GUILayout.Width(buttonSize)))
            {   
                rootNode.SetSelected(true);
            }

            if (GUILayout.Button("Deselect all", GUILayout.Width(buttonSize)))
            {
                if (rootNode.IsSemiSelected)
                {
                    rootNode.SetSelected(true);
                }
                rootNode.SetSelected(false);
            
            }
            
            if (GUILayout.Button("Run all", GUILayout.Width(buttonSize)))
            {
                foreach (var method in AllMethodNodes)
                {
                    method.ResetTestState();
                }
                RunTestsMode.SelectAllTests();
                PlayModeTestRunner.Run();
            }
            
            if (GUILayout.Button("Run smoke", GUILayout.Width(buttonSize)))
            {
                ClearAllSmokeMethods();
                RunTestsMode.SelectOnlySmokeTests();
                PlayModeTestRunner.Run();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            
            if (GUILayout.Button("Run selected", GUILayout.Width(width * 0.25f - buttonOffset)))
            {
                var children = rootNode.GetChildrenOfType<MethodNode>(true, false,
                    node => node.IsSelected);
                
                foreach (var testInfo in children)
                {
                    testInfo.ResetTestState();
                }

                RunTestsMode.SelectOnlySelectedTests();
                PlayModeTestRunner.Run();
            }

            if (runTestsGivenAmountOfTimes == -1)
            {
                runTestsGivenAmountOfTimes = PlayModeTestRunner.RunTestsGivenAmountOfTimes;
            }

            
            GUI.enabled = true;
            GUI.SetNextControlName("Repeating");

            int newTimes = EditorGUILayout.IntSlider("Repeat Tests N Times:", 
                runTestsGivenAmountOfTimes, 1,50);

            if (newTimes != runTestsGivenAmountOfTimes)
            {
                runTestsGivenAmountOfTimes = newTimes;

                PlayModeTestRunner.RunTestsGivenAmountOfTimes = newTimes;
            }

            EditorGUILayout.EndHorizontal();

            if (currentTimescale == -1)
            {
                currentTimescale = (int) PlayModeTestRunner.DefaultTimescale;
            }

            GUI.SetNextControlName("Timescale");

            int newTimescale = EditorGUILayout.IntSlider("Default Timescale:", currentTimescale, 1,20);

            if (newTimescale != currentTimescale)
            {
                currentTimescale = newTimescale;
                PlayModeTestRunner.DefaultTimescale = currentTimescale;
            }
            
            GUI.enabled = !Application.isPlaying;

            var oldValue = ShowAdvancedOptions;
            ShowAdvancedOptions = EditorGUILayout.Foldout(ShowAdvancedOptions, "Advanced Options");
            if (ShowAdvancedOptions)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                AdvancedSettings();
                GUILayout.EndVertical();
            }

            if (oldValue != ShowAdvancedOptions)
            {
                editorUpdateEnabledForFrames = 10;
            }

            EditorGUILayout.LabelField("Playmode tests: ", EditorStyles.boldLabel);
            GUI.enabled = true;
        }

        private void OnEditorUpdate()
        {
            if (editorUpdateEnabledForFrames > 0)
            {
                editorUpdateEnabledForFrames--;
                Repaint();
            }
        }
        
        
        void AdvancedSettings()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Make reference screenshot instead of comparing:");
            var oldValue = EditorGUILayout.Toggle("", PlayModeTestRunner.ForceMakeReferenceScreenshot);
            if (oldValue != PlayModeTestRunner.ForceMakeReferenceScreenshot)
            {
                PlayModeTestRunner.ForceMakeReferenceScreenshot = oldValue;
                isDirty = true;
                EditorUtility.SetDirty(PlayModeTestRunner.SerializedTests);
                
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var oldInstance = EditorTestResourcesFolder ? 
                EditorTestResourcesFolder.GetInstanceID() : -1;
            EditorTestResourcesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Editor Test Resources", 
                EditorTestResourcesFolder, 
                typeof(DefaultAsset), 
                false);
            var path = ValidateEditorResourceFolder();
            if (!string.IsNullOrEmpty(path) && 
                (oldInstance != EditorTestResourcesFolder.GetInstanceID() ||
                 string.IsNullOrEmpty(PlayModeTestRunner.EditorTestResourcesFolder)))
            {
                PlayModeTestRunner.EditorTestResourcesFolder = path;
                isDirty = true;
                EditorUtility.SetDirty(PlayModeTestRunner.SerializedTests);
            }
            
            oldInstance = BuildTestResourcesFolder ? 
                BuildTestResourcesFolder.GetInstanceID() : -1;
            BuildTestResourcesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Buil Test Resources", 
                BuildTestResourcesFolder, 
                typeof(DefaultAsset), 
                false);
            path = ValidateBuildResourceFolder();
            if (!string.IsNullOrEmpty(path) && 
                (oldInstance != BuildTestResourcesFolder.GetInstanceID() ||
                 string.IsNullOrEmpty(PlayModeTestRunner.BuildTestResourcesFolder)))
            {
                PlayModeTestRunner.BuildTestResourcesFolder = path;
                isDirty = true;
                EditorUtility.SetDirty(PlayModeTestRunner.SerializedTests);
            }

//            EditorGUILayout.BeginHorizontal();
//            GUILayout.Label("Update tests on every compilation: ");
//            UpdateTestsOnEveryCompilation = GUILayout.Toggle(UpdateTestsOnEveryCompilation, "");
//            GUILayout.FlexibleSpace();
//            EditorGUILayout.EndHorizontal();
//            EditorGUILayout.BeginHorizontal();
//            GUI.enabled = !UpdateTestsOnEveryCompilation;
//            if (GUILayout.Button("Force Update Tests"))
//            {
//                NodeFactory.Build();
//            }
//            GUILayout.Label("Can take several minutes...");
//            GUI.enabled = UpdateTestsOnEveryCompilation;
//            EditorGUILayout.EndHorizontal();
        }

        private string ValidateEditorResourceFolder()
        {
            if (EditorTestResourcesFolder != null)
            {
                var folderIncorrect = false;
                var assetPath = AssetDatabase.GetAssetPath(EditorTestResourcesFolder);
                assetPath = assetPath.Replace('\\', '/');
                if (EditorTestResourcesFolder.name != "Resources")
                {
                    Debug.LogError("Editor test resources folder " + assetPath +
                                   " is incorrect, it must be Resources folder");
                    folderIncorrect = true;
                }

                if (!assetPath.Contains("/Editor/"))
                {
                    Debug.LogError("Editor test resources folder " + assetPath +
                                   " is incorrect, Resource folder must be located somewhere inside Editor folder to prevent " +
                                   "adding test resources to build.");
                    folderIncorrect = true;
                }

                if (folderIncorrect)
                {
                    EditorTestResourcesFolder = null;
                }
                else
                {
                    return assetPath;
                }
            }

            return null;
        }
        
        private string ValidateBuildResourceFolder()
        {
            if (BuildTestResourcesFolder != null)
            {
                var folderIncorrect = false;
                var assetPath = AssetDatabase.GetAssetPath(BuildTestResourcesFolder);
                assetPath = assetPath.Replace('\\', '/');
                if (BuildTestResourcesFolder.name != "Resources")
                {
                    Debug.LogError("Build test resources folder " + assetPath +
                                   " is incorrect, it must be Resources folder");
                    folderIncorrect = true;
                }

                if (assetPath.Contains("/Editor/") || 
                    assetPath.Contains("/Editor"))
                {
                    Debug.LogError("Buld test resources folder " + assetPath +
                                   " is incorrect, Resource folder must NOT be located inside Editor folder");
                    folderIncorrect = true;
                }

                if (folderIncorrect)
                {
                    BuildTestResourcesFolder = null;
                }
                else
                {
                    return assetPath;
                }
            }
            return null;
        }

        private void ClearAllSmokeMethods()
        {
            foreach (var testMethod in AllMethodNodes)
            {
                if (testMethod.TestSettings.IsSmoke)
                {
                    testMethod.ResetTestState();
                }
            }
        }
    }
}
#endif