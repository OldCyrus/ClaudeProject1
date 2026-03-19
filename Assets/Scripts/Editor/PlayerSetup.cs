using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor utility — Tools > Setup Player
///
/// Creates:
///   • Player capsule  (CharacterController + all gameplay scripts + PlayerInput)
///   • CinemachineBrain on the Main Camera
///   • CinemachineFreeLook virtual camera  (3-rig orbit, collision avoidance)
///   • CameraTarget child object at shoulder height for the FreeLook to track
///   • CinemachineInputBridge on the Player to route new Input System → Cinemachine
/// </summary>
public static class PlayerSetup
{
    [MenuItem("Tools/Setup Player")]
    public static void Run()
    {
        // ── Guard: don't duplicate ─────────────────────────────────────────────
        var existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Player already exists",
                "A GameObject tagged 'Player' already exists.\nReplace it and its camera rig?",
                "Replace", "Cancel");
            if (!replace) return;

            // Also remove any existing Cinemachine rig.
            var oldRig = GameObject.Find("CM_FreeLook_Player");
            if (oldRig != null) Object.DestroyImmediate(oldRig);
            Object.DestroyImmediate(existing);
        }

        // ======================================================================
        // 1. PLAYER CAPSULE
        // ======================================================================
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag  = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.2f, 0.5f, 1.0f));
        player.GetComponent<Renderer>().sharedMaterial = mat;

        // ── CharacterController ────────────────────────────────────────────────
        var cc       = player.AddComponent<CharacterController>();
        cc.height    = 1.8f;
        cc.radius    = 0.35f;
        cc.center    = new Vector3(0f, 0f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.35f;

        // ── Gameplay scripts ───────────────────────────────────────────────────
        player.AddComponent<PlayerStats>();
        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerCombat>();
        player.AddComponent<PlayerInteraction>();
        player.AddComponent<CinemachineInputBridge>();   // routes mouse to Cinemachine

        // ── New Input System — PlayerInput ─────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
        var pi = player.AddComponent<PlayerInput>();

        string[] guids = AssetDatabase.FindAssets("PlayerInputActions t:InputActionAsset");
        if (guids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var    actions   = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            pi.actions              = actions;
            pi.defaultActionMap     = "Player";
            pi.notificationBehavior = PlayerNotifications.SendMessages;
        }
        else
        {
            Debug.LogWarning("[PlayerSetup] PlayerInputActions asset not found — " +
                             "assign it manually on the PlayerInput component.");
        }
#endif

        // ── Player physics layer ───────────────────────────────────────────────
        EnsureLayer("Player", out int playerLayer);
        player.layer = playerLayer;
        Physics.IgnoreLayerCollision(playerLayer, playerLayer, true);

        // ── Camera-target child (shoulder / head height) ───────────────────────
        var camTarget     = new GameObject("CameraTarget");
        camTarget.transform.SetParent(player.transform);
        camTarget.transform.localPosition = new Vector3(0f, 1.5f, 0f);  // shoulder height

        // ======================================================================
        // 2. MAIN CAMERA  +  CinemachineBrain
        // ======================================================================
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam   = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        // Remove any old custom follow script we may have added previously.
        var oldTpc = mainCam.GetComponent<ThirdPersonCamera>();
        if (oldTpc != null) Object.DestroyImmediate(oldTpc);

        mainCam.nearClipPlane = 0.1f;
        mainCam.fieldOfView   = 60f;
        mainCam.transform.position = player.transform.position + new Vector3(0f, 4f, -7f);
        mainCam.transform.LookAt(camTarget.transform.position);

        // CinemachineBrain drives the camera from the highest-priority virtual camera.
        var brain = mainCam.GetComponent<CinemachineBrain>()
                 ?? mainCam.gameObject.AddComponent<CinemachineBrain>();
        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut, 0.5f);

        // ======================================================================
        // 3. CINEMACHINE FREELOOK VIRTUAL CAMERA
        // ======================================================================
        var rigGO    = new GameObject("CM_FreeLook_Player");
        var freeLook = rigGO.AddComponent<CinemachineFreeLook>();

        freeLook.Follow = camTarget.transform;   // position driver
        freeLook.LookAt = camTarget.transform;   // aim target
        freeLook.m_Priority = 10;

        // ── Lens ──────────────────────────────────────────────────────────────
        freeLook.m_Lens.FieldOfView   = 60f;
        freeLook.m_Lens.NearClipPlane = 0.1f;
        freeLook.m_Lens.FarClipPlane  = 300f;

        // ── Three orbit rigs (top / middle / bottom) ───────────────────────────
        //   Height = vertical offset from Follow target
        //   Radius = horizontal distance at that rig
        freeLook.m_Orbits = new CinemachineFreeLook.Orbit[]
        {
            new CinemachineFreeLook.Orbit { m_Height =  4.5f, m_Radius = 3.5f },  // top
            new CinemachineFreeLook.Orbit { m_Height =  1.8f, m_Radius = 6.0f },  // middle
            new CinemachineFreeLook.Orbit { m_Height = -0.5f, m_Radius = 3.0f },  // bottom
        };
        freeLook.m_SplineCurvature = 0.3f;      // gentle spline between rigs

        // ── Binding mode (world-space follow, no locking) ────────────────────
        freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;

        // ── Input axes ────────────────────────────────────────────────────────
        // CinemachineInputBridge overrides CinemachineCore.GetInputAxis to read
        // from Mouse.current.delta, so we just name the axes "Mouse X" / "Mouse Y".
        freeLook.m_XAxis.m_InputAxisName = "Mouse X";
        freeLook.m_XAxis.m_MaxSpeed      = 300f;
        freeLook.m_XAxis.m_AccelTime     = 0.1f;
        freeLook.m_XAxis.m_DecelTime     = 0.1f;
        freeLook.m_XAxis.m_InvertInput   = false;

        freeLook.m_YAxis.m_InputAxisName = "Mouse Y";
        freeLook.m_YAxis.m_MaxSpeed      = 2f;
        freeLook.m_YAxis.m_AccelTime     = 0.1f;
        freeLook.m_YAxis.m_DecelTime     = 0.1f;
        freeLook.m_YAxis.m_InvertInput   = false;
        freeLook.m_YAxis.Value           = 0.5f;   // start at middle rig

        // ── Recenter (auto-center behind player after idle) ───────────────────
        freeLook.m_RecenterToTargetHeading.m_enabled    = true;
        freeLook.m_RecenterToTargetHeading.m_WaitTime   = 2.0f;
        freeLook.m_RecenterToTargetHeading.m_RecenteringTime = 1.0f;
        freeLook.m_YAxisRecentering.m_enabled           = false;  // keep vertical position

        // ── Collision avoidance (Cinemachine Collider extension) ─────────────
        var col = rigGO.AddComponent<CinemachineCollider>();
        col.m_Strategy         = CinemachineCollider.ResolutionStrategy.PullCameraForward;
        col.m_CollideAgainst   = ~LayerMask.GetMask("Player");
        col.m_IgnoreTag        = "Player";
        col.m_SmoothingTime    = 0.2f;
        col.m_CameraRadius     = 0.15f;
        col.m_DampingWhenOccluded = 0.5f;

        // ======================================================================
        // 4. CONFIGURE PER-RIG COMPOSERS (aim at shoulder, not dead center)
        // ======================================================================
        // FreeLook rigs are child GameObjects named "Top Rig", "Mid Rig", "Bot Rig".
        // We must call GetRig() after the component is added and laid out.
        for (int i = 0; i < 3; i++)
        {
            var rig      = freeLook.GetRig(i);
            var composer = rig?.GetCinemachineComponent<CinemachineComposer>();
            if (composer == null) continue;

            composer.m_TrackedObjectOffset = new Vector3(0.4f, 0f, 0f);  // slight right offset (over-shoulder)
            composer.m_HorizontalDamping   = 0.15f;
            composer.m_VerticalDamping     = 0.15f;
            composer.m_ScreenX             = 0.55f;   // push subject slightly left of centre
            composer.m_ScreenY             = 0.45f;
            composer.m_DeadZoneWidth       = 0.05f;
            composer.m_DeadZoneHeight      = 0.05f;
        }

        // ======================================================================
        // 5. FINALISE
        // ======================================================================
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = player;
        EditorGUIUtility.PingObject(player);

        Debug.Log("[PlayerSetup] Done! Player + Cinemachine FreeLook created. " +
                  "Press Ctrl+S to save the scene, then press Play.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void EnsureLayer(string layerName, out int layerIndex)
    {
        layerIndex = LayerMask.NameToLayer(layerName);
        if (layerIndex != -1) return;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                layerIndex = i;
                Debug.Log($"[PlayerSetup] Created layer '{layerName}' at index {i}.");
                return;
            }
        }
        Debug.LogWarning("[PlayerSetup] No free layer slot — using Default (0).");
        layerIndex = 0;
    }
}
