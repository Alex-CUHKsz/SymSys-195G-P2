#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using MurderVilla.Dialogue;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MurderVilla.Editor
{
    [InitializeOnLoad]
    public static class CharacterSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/VillaHorrorPrototype.unity";
        private const string CharacterRootName = "Story Characters ABCDEF";
        private const string AssetRoot =
            "Assets/_Project/ThirdParty/Quaternius/UniversalBaseCharacters";
        private const string MaleModel = AssetRoot + "/Models/Superhero_Male_FullBody.fbx";
        private const string FemaleModel = AssetRoot + "/Models/Superhero_Female_FullBody.fbx";
        private const string AnimationModel =
            "Assets/_Project/ThirdParty/Quaternius/UniversalAnimationLibrary/Animations/UAL1_Standard.fbx";
        private const string ControllerPath =
            "Assets/_Project/Art/Animations/NPCIdle.controller";
        private const string PreferredIdle = "Armature|Idle_Loop";

        private static readonly string[] HairModels =
        {
            "Hair_Buns.fbx",
            "Hair_SimpleParted.fbx",
            "Hair_Long.fbx",
            "Hair_Buzzed.fbx",
            "Hair_BuzzedFemale.fbx",
            "Hair_Beard.fbx",
        };

        private static bool buildQueued;

        static CharacterSceneBuilder()
        {
            QueueBuild();
        }

        [MenuItem("Murder in Old Villa/Add Story Characters (ABCDEF)")]
        public static void BuildFromMenu()
        {
            Debug.Log("[CharacterBuilder] Menu command received. isPlaying=" +
                EditorApplication.isPlayingOrWillChangePlaymode +
                " isCompiling=" + EditorApplication.isCompiling +
                " isUpdating=" + EditorApplication.isUpdating);
            BuildCharacters(true);
        }

        private static void QueueBuild()
        {
            if (buildQueued)
                return;

            buildQueued = true;
            EditorApplication.delayCall += TryBuildAfterImport;
        }

        private static void TryBuildAfterImport()
        {
            buildQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueBuild();
                return;
            }

            BuildCharacters(false);
        }

        private static void BuildCharacters(bool forceRebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            if (!File.Exists(ScenePath) || !File.Exists(MaleModel) ||
                !File.Exists(FemaleModel))
                return;

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (InvalidOperationException)
            {
                // Unity is still transitioning — retry later
                QueueBuild();
                return;
            }
            GameObject existing = FindNamed(CharacterRootName);
            Debug.Log($"[CharacterBuilder] Existing '{CharacterRootName}': {(existing != null ? "found" : "null")}, forceRebuild={forceRebuild}");

            if (existing != null && !forceRebuild)
            {
                Debug.Log("[CharacterBuilder] Skipping — characters already exist.");
                return;
            }

            if (existing != null)
            {
                Debug.Log("[CharacterBuilder] Destroying existing character root.");
                UnityEngine.Object.DestroyImmediate(existing);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RuntimeAnimatorController controller = CreateIdleController();
            GameObject male = AssetDatabase.LoadAssetAtPath<GameObject>(MaleModel);
            GameObject female = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModel);
            if (male == null || female == null)
            {
                Debug.LogWarning("Character FBX files are still importing. Try the character menu again.");
                return;
            }

            GameObject root = new(CharacterRootName);
            Transform player = FindNamed("Player")?.transform;
            Vector3 playerPosition = player != null ? player.position : new Vector3(-9f, 1f, -10f);
            Vector3 forward = player != null
                ? Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.1f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Hardcoded verified positions from YAML editing
            Vector3 amyPosition = new Vector3(-14.0f, 0.02f, -4.0f);
            Vector3 benPosition = new Vector3(-2.0f, 0.02f, -4.0f);
            Vector3 cocoPosition = new Vector3(-9.0f, 0.02f, -2.0f);
            Vector3 deanPosition = new Vector3(-12.0f, 3.02f, -18.0f);
            Vector3 ellaPosition = new Vector3(-4.0f, 3.02f, -5.0f);
            Vector3 felixPosition = FindAnchorPosition("GothicBed",
                new Vector3(-18.0f, 0.72f, -23.0f), true);

            Debug.Log($"[CharacterBuilder] Placing Amy at {amyPosition}");
            Debug.Log($"[CharacterBuilder] Placing Ben at {benPosition}");
            Debug.Log($"[CharacterBuilder] Placing Coco at {cocoPosition}");
            Debug.Log($"[CharacterBuilder] Placing Dean at {deanPosition}");
            Debug.Log($"[CharacterBuilder] Placing Ella at {ellaPosition}");
            Debug.Log($"[CharacterBuilder] Placing Felix at {felixPosition}");

            CreateCharacter(root.transform, "Amy", female, HairModels[0], amyPosition,
                0.98f, new Color(0.46f, 0.18f, 0.20f), controller, true,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Why aren't you wearing clothes?",
                        responseText = "That's very rude of you to ask.",
                    },
                    new DialogueBranch
                    {
                        questionText = "Where were you around 21:50 that night?",
                        qaSequence = new QAPair[]
                        {
                            new QAPair
                            {
                                question = "Where were you around 21:50 that night?",
                                answer = "I passed by the master bedroom door and went straight to the living room. I didn't stay long.",
                            },
                            new QAPair
                            {
                                question = "What do you mean by \"passed by\"? Did you go inside?",
                                answer = "No, I never went into that room. I didn't go in there once that whole night.",
                            },
                            new QAPair
                            {
                                question = "Okay, so you stayed in the living room after that?",
                                answer = "Yes, I didn't go back upstairs after returning to the living room. I didn't go up to the second floor at all after 22:00 — not once.",
                            },
                            new QAPair
                            {
                                question = "Felix's birthday cake seemed to be cut pretty late. Do you remember what was prepared that night?",
                                answer = "...(pause) I don't know. Anyway, I never touched the milk. I had nothing to do with that cup of milk at all.",
                            },
                            new QAPair
                            {
                                question = "(frowns slightly, doesn't press about the milk) ...Alright. Did you see Coco that night?",
                                answer = "We didn't really cross paths that night. I think she was in the living room talking with Dean the whole time — I wasn't really paying attention.",
                            },
                        },
                    },
                });

            CreateCharacter(root.transform, "Ben", male, HairModels[1], benPosition,
                1.02f, new Color(0.16f, 0.26f, 0.38f), controller, true,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Why aren't you wearing clothes?",
                        responseText = "Excuse me? That's none of your business.",
                    },
                    new DialogueBranch
                    {
                        questionText = "Did you go up to the second floor that night?",
                        qaSequence = new QAPair[]
                        {
                            new QAPair
                            {
                                question = "Did you go up to the second floor that night?",
                                answer = "Yeah, I went up sometime after 22:00 to grab something. I glanced toward the master bedroom door on my way.",
                            },
                            new QAPair
                            {
                                question = "What time exactly? What did you see?",
                                answer = "Around 22:18, I think — I wasn't really paying attention to the time. The room was dark, no lights on.",
                            },
                            new QAPair
                            {
                                question = "Dark? Then how did you know someone was in there?",
                                answer = "I heard the sound of newspaper pages turning — it kept going, kind of a rustling sound, pretty clear.",
                            },
                            new QAPair
                            {
                                question = "So you assumed your uncle was still awake?",
                                answer = "Yeah, I figured he was reading the paper in the dark, or just flipping through it absentmindedly. Didn't think much of it, so I just went downstairs.",
                            },
                            new QAPair
                            {
                                question = "How long did you stay at the door? Did you knock or call out to him?",
                                answer = "Just a few seconds. I didn't knock — it was his birthday, so I didn't want to bother him. I figured he probably just wanted some time alone.",
                            },
                        },
                    },
                });

            CreateCharacter(root.transform, "Coco", female, HairModels[2], cocoPosition,
                1.01f, new Color(0.24f, 0.15f, 0.34f), controller, true,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Why aren't you wearing clothes?",
                        responseText = "How dare you! I'm calling my lawyer.",
                    },
                    new DialogueBranch
                    {
                        questionText = "Where were you between 22:00 and 22:30 that night?",
                        qaSequence = new QAPair[]
                        {
                            new QAPair
                            {
                                question = "Where were you between 22:00 and 22:30 that night?",
                                answer = "I was in the living room, talking with Dean. We were just chatting.",
                            },
                            new QAPair
                            {
                                question = "Did you leave the room at any point during that time?",
                                answer = "No, not once. I didn't go upstairs at all that night.",
                            },
                            new QAPair
                            {
                                question = "Not even for a moment — to use the restroom, or grab something?",
                                answer = "No, nothing like that. I was there the whole time — you can ask Dean.",
                            },
                            new QAPair
                            {
                                question = "How was your relationship with Felix?",
                                answer = "It was... fine. We didn't always see eye to eye, but that's just family stuff. Nothing serious.",
                            },
                            new QAPair
                            {
                                question = "Did you touch the curtains in his bedroom recently, for any reason?",
                                answer = "(slightly defensive) Why would I? I haven't even been in his room in weeks.",
                            },
                        },
                    },
                });

            CreateCharacter(root.transform, "Dean", male, HairModels[3], deanPosition,
                1.04f, new Color(0.15f, 0.28f, 0.22f), controller, true,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Why aren't you wearing clothes?",
                        responseText = "...I don't have to answer that. Talk to my lawyer.",
                    },
                    new DialogueBranch
                    {
                        questionText = "Where were you between 22:00 and 22:30 that night?",
                        qaSequence = new QAPair[]
                        {
                            new QAPair
                            {
                                question = "Where were you between 22:00 and 22:30 that night?",
                                answer = "I was in the living room, with Coco. We talked for a while.",
                            },
                            new QAPair
                            {
                                question = "Did either of you leave the room?",
                                answer = "No. We were both there the whole time.",
                            },
                            new QAPair
                            {
                                question = "Did you check on Felix at any point?",
                                answer = "I went up to patrol around 22:30, like I always do.",
                            },
                            new QAPair
                            {
                                question = "What did you find?",
                                answer = "Everything was quiet. I assumed he'd gone to sleep, so I didn't go in.",
                            },
                            new QAPair
                            {
                                question = "Is there anything else you remember from that night — anything unusual?",
                                answer = "(hesitates) No... nothing unusual. It was a normal night.",
                            },
                        },
                    },
                });

            CreateCharacter(root.transform, "Ella", female, HairModels[4], ellaPosition,
                0.96f, new Color(0.42f, 0.36f, 0.22f), controller, true,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Why aren't you wearing clothes?",
                        responseText = "Excuse me?! I'm the housekeeper, not a suspect. Show some respect.",
                    },
                    new DialogueBranch
                    {
                        questionText = "What were you doing around 21:45 that night?",
                        qaSequence = new QAPair[]
                        {
                            new QAPair
                            {
                                question = "What were you doing around 21:45 that night?",
                                answer = "I'd just finished cleaning the second floor, and went down to the kitchen to warm up some milk for Felix.",
                            },
                            new QAPair
                            {
                                question = "Did you notice anything unusual on your way down?",
                                answer = "There was someone near the staircase, moving pretty fast. I couldn't tell who it was — just a quick shape passing by.",
                            },
                            new QAPair
                            {
                                question = "What time did you deliver the milk?",
                                answer = "Right at 22:00. Felix was sitting up, reading the newspaper, lamp was on. Looked completely normal.",
                            },
                            new QAPair
                            {
                                question = "Did you go back upstairs after that?",
                                answer = "No, I stayed in the kitchen the rest of the night, cleaning up.",
                            },
                            new QAPair
                            {
                                question = "Did you see anyone else go upstairs after you came down?",
                                answer = "No, I didn't see anyone. I was busy in the kitchen — wasn't really watching the stairs.",
                            },
                        },
                    },
                });

            GameObject felix = CreateCharacter(root.transform, "Felix", male,
                HairModels[5], felixPosition, 1.05f, new Color(0.22f, 0.22f, 0.24f),
                null, false,
                new DialogueBranch[]
                {
                    new DialogueBranch
                    {
                        questionText = "Examine the body",
                        responseText = "The curtain-cord marks indicate mechanical strangulation. There are no signs that Felix willingly tightened the cord himself.",
                    },
                });
            felix.GetComponent<NPCDialogue>().SetMonologue(
                "It was my birthday that night. When Ella brought up the milk, I was sitting up in bed reading the newspaper, lamp on, everything normal.\n\n" +
                "I remember taking a few sips of the milk. It tasted a little off, but I didn't think much of it — it was my birthday, after all.\n\n" +
                "Then... my body started feeling heavy. The newspaper slipped out of my hands. My eyelids kept getting heavier, and I couldn't keep them open no matter what. The room started to blur, and the sounds around me felt far away, distant.\n\n" +
                "I tried to call out, but no sound came.\n\n" +
                "Then... nothing. Just darkness.");
            Transform felixVisual = felix.transform.Find("Visual");
            if (felixVisual != null)
            {
                felixVisual.localRotation = Quaternion.Euler(0f, 0f, 90f);
                felixVisual.localPosition = new Vector3(0f, 0.55f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterBuilder] Scene saved. Root '{CharacterRootName}' child count: {root.transform.childCount}");
            Debug.Log("Added Amy, Ben, Coco, Dean, Ella and Felix with dialogue and idle motion.");
        }

        private static GameObject CreateCharacter(Transform parent, string characterName,
            GameObject bodyPrefab, string hairFile, Vector3 position, float heightScale,
            Color accent, RuntimeAnimatorController controller, bool animate,
            DialogueBranch[] dialogue)
        {
            GameObject character = new($"NPC {characterName}");
            character.transform.SetParent(parent);
            character.transform.position = position;

            GameObject visual = new("Visual");
            visual.transform.SetParent(character.transform, false);
            visual.transform.localScale = Vector3.one * heightScale;

            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(bodyPrefab, visual.transform);
            body.name = characterName + " Body";
            body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ApplyCharacterMaterials(body, characterName, accent, false);
            ConfigureAnimator(body, controller, animate);

            string hairPath = AssetRoot + "/Hairstyles/" + hairFile;
            GameObject hairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hairPath);
            if (hairPrefab != null)
            {
                GameObject hair = (GameObject)PrefabUtility.InstantiatePrefab(hairPrefab, visual.transform);
                hair.name = characterName + " Hair";
                hair.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                ApplyCharacterMaterials(hair, characterName, accent, true);
                ConfigureAnimator(hair, controller, animate);
            }

            CapsuleCollider collider = character.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f * heightScale, 0f);
            collider.height = 1.8f * heightScale;
            collider.radius = 0.36f;

            NPCIdleMotion motion = character.AddComponent<NPCIdleMotion>();
            motion.Configure(visual.transform, animate);
            NPCDialogue npcDialogue = character.AddComponent<NPCDialogue>();
            npcDialogue.Configure(characterName, dialogue, motion, animate);

            Vector3 faceCenter = Camera.main != null
                ? Camera.main.transform.position
                : character.transform.position + Vector3.forward;
            Vector3 direction = faceCenter - character.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                character.transform.rotation = Quaternion.LookRotation(direction.normalized);

            return character;
        }

        private static void ConfigureAnimator(GameObject model,
            RuntimeAnimatorController controller, bool animate)
        {
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = animate ? controller : null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        private static RuntimeAnimatorController CreateIdleController()
        {
            if (!File.Exists(AnimationModel))
                return null;

            EnsureFolder("Assets/_Project/Art/Animations");
            EnsureIdleClipsLoop();
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                bool valid = existing.layers.Length > 0 &&
                    existing.layers[0].stateMachine.defaultState != null &&
                    existing.layers[0].stateMachine.defaultState.motion != null &&
                    existing.layers[0].stateMachine.defaultState.motion.name == PreferredIdle;
                if (valid)
                    return existing;

                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(AnimationModel)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => clip.name == PreferredIdle ? 0 :
                    clip.name.Contains("Idle_Talking") ? 1 :
                    clip.name.Contains("Idle") ? 2 : 3)
                .FirstOrDefault();
            if (idle == null)
                return null;

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, idle);
            controller.layers[0].stateMachine.defaultState.speed = 0.82f;
            return controller;
        }

        private static void EnsureIdleClipsLoop()
        {
            ModelImporter importer = AssetImporter.GetAtPath(AnimationModel) as ModelImporter;
            if (importer == null)
                return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.name.Contains("Idle") || clip.loopTime)
                    continue;
                clip.loopTime = true;
                clip.loopPose = true;
                changed = true;
            }

            if (!changed)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void ApplyCharacterMaterials(GameObject model, string name,
            Color accent, bool hair)
        {
            EnsureFolder("Assets/_Project/Art/Materials/Characters");
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                Material[] replacements = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string materialName = source[i] != null ? source[i].name : "Body";
                    string key = hair || materialName.ToLowerInvariant().Contains("hair")
                        ? "Hair"
                        : materialName.ToLowerInvariant().Contains("eye") ? "Eyes" : "Body";
                    replacements[i] = CharacterMaterial(name, key, accent);
                }
                renderer.sharedMaterials = replacements;
            }
        }

        private static Material CharacterMaterial(string characterName, string part,
            Color accent)
        {
            string path = $"Assets/_Project/Art/Materials/Characters/{characterName}_{part}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = characterName + " " + part };
            Color color = part switch
            {
                "Hair" => Color.Lerp(accent, Color.black, 0.58f),
                "Eyes" => new Color(0.22f, 0.12f, 0.06f),
                _ => Color.Lerp(Color.white, accent, 0.12f),
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.28f);

            Texture2D texture = CharacterTexture(characterName, part);
            if (texture != null)
            {
                if (material.HasProperty("_BaseColorMap"))
                    material.SetTexture("_BaseColorMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D CharacterTexture(string characterName, string part)
        {
            const string textures = AssetRoot + "/Textures/";
            if (part == "Eyes")
                return AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "T_Eye_Brown.png");
            if (part == "Hair")
            {
                string hairTexture = characterName is "Amy" or "Dean" or "Felix"
                    ? "T_Hair_1_BaseColor.png"
                    : "T_Hair_2_BaseColor.png";
                return AssetDatabase.LoadAssetAtPath<Texture2D>(textures + hairTexture);
            }

            bool female = characterName is "Amy" or "Coco" or "Ella";
            bool dark = characterName is "Coco" or "Dean";
            string bodyTexture = female
                ? dark ? "T_Superhero_Female_Dark_BaseColor.png"
                    : "T_Superhero_Female_Light_BaseColor.png"
                : dark ? "T_Superhero_Male_Dark.png"
                    : "T_Superhero_Male_Ligh.png";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(textures + bodyTexture);
        }

        private static Vector3 FindAnchorPosition(string objectName, Vector3 fallback,
            bool useTop)
        {
            GameObject anchor = FindClosestNamed(objectName, fallback);
            if (anchor == null)
                return fallback; // Use verified coordinates directly

            Renderer[] renderers = anchor.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return fallback;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);

            if (useTop)
                return new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);

            Vector3 edge = bounds.center + Vector3.right * Mathf.Min(1.2f, bounds.extents.x);
            return PlaceOnFloor(edge);
        }

        private static Vector3 PlaceOnFloor(Vector3 point)
        {
            Physics.SyncTransforms();
            Vector3 origin = point + Vector3.up * 7f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 15f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;
            return point;
        }

        private static GameObject FindClosestNamed(string name, Vector3 near)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(candidate => candidate.name == name)
                .OrderBy(candidate => (candidate.position - near).sqrMagnitude)
                .Select(candidate => candidate.gameObject)
                .FirstOrDefault();
        }

        private static GameObject FindNamed(string name)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.name == name)?.gameObject;
        }

        private static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, pieces[i]);
                current = next;
            }
        }
    }
}
#endif
