#if UNITY_EDITOR
using System.IO;
using Investigation;
using MurderVilla.Interaction;
using MurderVilla.InvestigationSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MurderVilla.Editor
{
    [InitializeOnLoad]
    public static class HorrorVillaSceneBuilder
    {
        private const string SourceScene = "Assets/FpsHorrorKit/Scenes/MainScene.unity";
        private const string TargetScene = "Assets/_Project/Scenes/VillaHorrorPrototype.unity";
        private const string EvidencePath = "Assets/_Project/Data/Evidence";
        private const string EvidenceMaterial =
            "Assets/_Project/Art/Materials/Evidence.mat";

        static HorrorVillaSceneBuilder()
        {
            EditorApplication.delayCall += BuildOnImport;
        }

        private static void BuildOnImport()
        {
            if (Application.isBatchMode || !File.Exists(SourceScene) ||
                File.Exists(TargetScene))
                return;

            Build();
        }

        [MenuItem("Murder in Old Villa/Build Horror Villa Scene")]
        public static void Build()
        {
            if (!File.Exists(SourceScene))
            {
                Debug.LogWarning(
                    "FPS Horror Game Starter Pack is not installed. " +
                    $"Expected scene: {SourceScene}");
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null)
                AssetDatabase.DeleteAsset(TargetScene);

            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
            {
                Debug.LogError($"Could not copy {SourceScene} to {TargetScene}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            RemovePrototypeThreat();

            GameObject player = FindNamedObject("Player");
            Camera camera = FindMainCamera();
            if (player == null || camera == null)
            {
                Debug.LogError("The horror pack Player or Main Camera could not be found.");
                return;
            }

            FirstPersonInteractor interactor =
                player.GetComponent<FirstPersonInteractor>() ??
                player.AddComponent<FirstPersonInteractor>();
            interactor.Configure(camera);

            CreateCaseSystems();
            CreateEvidence(player.transform);
            CreateDetectiveHud(interactor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TargetScene, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Horror villa detective scene created at {TargetScene}");
        }

        private static void RemovePrototypeThreat()
        {
            GameObject killer = FindNamedObject("Killer");
            if (killer != null)
                Object.DestroyImmediate(killer);
        }

        private static GameObject FindNamedObject(string objectName)
        {
            foreach (Transform candidate in
                     Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }

            return null;
        }

        private static Camera FindMainCamera()
        {
            foreach (Camera candidate in
                     Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.CompareTag("MainCamera"))
                    return candidate;
            }

            return Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        }

        private static void CreateCaseSystems()
        {
            GameObject systems = new("Detective Case Systems");
            systems.AddComponent<EvidenceLog>();
            systems.AddComponent<Investigation.SuspectInterviewLog>();
            systems.AddComponent<CaseVerdictUI>();
        }

        private static void CreateEvidence(Transform player)
        {
            GameObject root = new("Murder Case Evidence");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(EvidenceMaterial);

            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (forward.sqrMagnitude < 0.1f)
                forward = Vector3.forward;

            EvidenceObject("Drugged Milk Cup", PrimitiveType.Cylinder, root.transform,
                PlaceOnFloor(player.position + forward * 4f + right * 1.5f, 0.12f),
                new Vector3(0.18f, 0.16f, 0.18f), material,
                Definition("milk_cup", "Drugged Milk Cup",
                    "Sedative residue remains in the milk. It was added before Ella delivered it.",
                    SuspectId.Amy));

            EvidenceObject("Untouched Newspaper", PrimitiveType.Cube, root.transform,
                PlaceOnFloor(player.position + forward * 7f - right * 1.5f, 0.06f),
                new Vector3(0.65f, 0.04f, 0.45f), material,
                Definition("flat_newspaper", "Untouched Newspaper",
                    "The paper is flat. The reported rustling in a dark room was staged.",
                    SuspectId.Ben));

            EvidenceObject("Curtain Cord", PrimitiveType.Cylinder, root.transform,
                PlaceOnFloor(player.position + forward * 10f + right * 2f, 0.55f),
                new Vector3(0.055f, 0.55f, 0.055f), material,
                Definition("curtain_cord", "Curtain Cord",
                    "The murder weapon. Only Coco's fingerprints are present.",
                    SuspectId.Coco));

            EvidenceObject("Dean Private USB", PrimitiveType.Cube, root.transform,
                PlaceOnFloor(player.position + forward * 13f - right * 2f, 0.08f),
                new Vector3(0.16f, 0.06f, 0.32f), material,
                Definition("wang_usb", "Dean's Private USB",
                    "The original footage shows Coco going upstairs alone at 22:15.",
                    SuspectId.Dean));

            EvidenceObject("Living-Room Clock", PrimitiveType.Cube, root.transform,
                PlaceOnFloor(player.position + forward * 5f - right * 3.5f, 0.5f),
                new Vector3(0.32f, 0.5f, 0.12f), material,
                Definition("clock", "Living-Room Clock",
                    "The clock stopped once. Coco's account of the evening jumps " +
                    "from 22:14 to 22:22 with no explanation for the gap.",
                    SuspectId.Coco, "curtain_cord"));

            EvidenceObject("Breaker Log", PrimitiveType.Cube, root.transform,
                PlaceOnFloor(player.position + forward * 9f + right * 3.5f, 0.05f),
                new Vector3(0.3f, 0.03f, 0.22f), material,
                Definition("log", "Breaker Log",
                    "At 22:12 Dean left the living room to deal with a tripped " +
                    "breaker, leaving Coco unwatched during the murder window.",
                    SuspectId.Dean, "curtain_cord"));
        }

        private static Vector3 PlaceOnFloor(Vector3 around, float height)
        {
            Physics.SyncTransforms();
            Vector3 origin = around + Vector3.up * 8f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * height;

            return new Vector3(around.x, around.y - 0.75f + height, around.z);
        }

        private static void EvidenceObject(string name, PrimitiveType primitive,
            Transform parent, Vector3 position, Vector3 scale, Material material,
            EvidenceDefinition definition)
        {
            GameObject evidence = GameObject.CreatePrimitive(primitive);
            evidence.name = name;
            evidence.transform.SetParent(parent);
            evidence.transform.position = position;
            evidence.transform.localScale = scale;
            if (material != null)
                evidence.GetComponent<Renderer>().sharedMaterial = material;

            evidence.AddComponent<EvidencePickup>().Configure(definition);
        }

        private static EvidenceDefinition Definition(string id, string title,
            string description, SuspectId suspect, string contradictsEvidenceId = null)
        {
            string path = $"{EvidencePath}/{id}.asset";
            EvidenceDefinition definition =
                AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EvidenceDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.id = id;
            definition.title = title;
            definition.description = description;
            definition.relatedSuspect = suspect;
            definition.contradictsEvidenceId = contradictsEvidenceId;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void CreateDetectiveHud(FirstPersonInteractor interactor)
        {
            GameObject canvasObject = new("Detective HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            Text title = TextElement(canvasObject.transform, "Case Title",
                "MURDER IN THE OLD VILLA", 22, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(420f, 42f));

            Text counter = TextElement(canvasObject.transform, "Evidence Counter",
                "Evidence: 0 / 6", 18, TextAnchor.UpperLeft);
            SetRect(counter.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -66f), new Vector2(300f, 34f));
            counter.gameObject.AddComponent<EvidenceCounterUI>().Configure(counter, 6);

            Text prompt = TextElement(canvasObject.transform, "Interaction Prompt",
                string.Empty, 20, TextAnchor.MiddleCenter);
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f),
                new Vector2(640f, 44f));
            prompt.gameObject.AddComponent<InteractionPromptUI>()
                .Configure(interactor, prompt);

            Text controls = TextElement(canvasObject.transform, "Controls",
                "WASD Move  |  Shift Sprint  |  Space Jump  |  Mouse Look  |  E Interact  |  V Case Board",
                15, TextAnchor.LowerLeft);
            controls.color = new Color(1f, 1f, 1f, 0.72f);
            SetRect(controls.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 18f), new Vector2(760f, 32f));
        }

        private static Text TextElement(Transform parent, string name, string text,
            int size, TextAnchor alignment)
        {
            GameObject target = new(name);
            target.transform.SetParent(parent, false);
            Text label = target.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
#endif
