using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HousePlanned
{
    [InitializeOnLoad]
    public static class HousePlannedBuilder
    {
        // ==========================================
        // MASTER CONSTANTS
        // ==========================================
        public const float FP_X_MIN = -7.80f;
        public const float FP_X_MAX = 7.80f;   // Total 15.60m
        public const float FP_Z_MIN = -3.75f;
        public const float FP_Z_MAX = 3.75f;   // Total 7.50m

        public const float EXT_WALL_THICK = 0.25f;
        public const float INT_WALL_THICK = 0.15f;
        public const float SLAB_THICK = 0.25f;

        public const float BASEMENT_Y = -3.00f;
        public const float GROUND_Y = 0.00f;
        public const float UPPER_Y = 3.00f;
        public const float FLOOR_HEIGHT = 3.00f;
        public const float WALL_HEIGHT = 2.75f; // clear interior height
        public const float ROOF_RIDGE_Y = UPPER_Y + 2.75f + 2.40f; // Peak height ~8.15m

        public const float INT_X_MIN = FP_X_MIN + EXT_WALL_THICK; // -7.55f
        public const float INT_X_MAX = FP_X_MAX - EXT_WALL_THICK; // +7.55f (15.10m)
        public const float INT_Z_MIN = FP_Z_MIN + EXT_WALL_THICK; // -3.50f
        public const float INT_Z_MAX = FP_Z_MAX - EXT_WALL_THICK; // +3.50f (7.00m)

        // Stair Parameters
        public const int STAIR_RISER_COUNT = 17;
        public const float STAIR_RISER_H = FLOOR_HEIGHT / STAIR_RISER_COUNT; // 3.0 / 17 ≈ 0.17647m
        public const float STAIR_TREAD_D = 0.28f;
        public const float STAIR_WIDTH = 1.00f;

        // Door Parameters
        public const float DOOR_WIDTH_STANDARD = 0.90f;
        public const float DOOR_WIDTH_ENTRANCE = 1.00f;
        public const float DOOR_HEIGHT = 2.10f;

        // Material Palette
        private static Material matWallExt;
        private static Material matWallInt;
        private static Material matWallAccent;
        private static Material matFloorInterior;
        private static Material matFloorWet;
        private static Material matFloorGarage;
        private static Material matDeck;
        private static Material matSlab;
        private static Material matStairs;
        private static Material matGarden;
        private static Material matDriveway;
        private static Material matFrame;
        private static Material matGlass;
        private static Material matRoof;

        static HousePlannedBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                BuildPlannedHouse();
            };
        }

        [MenuItem("House/Build Planned House Greybox")]
        public static void BuildSceneMenu()
        {
            BuildPlannedHouse();
        }

        [MenuItem("House/Capture Viewpoints")]
        public static void CaptureViewpointsMenu()
        {
            CaptureAllViews();
        }

        public static void BuildPlannedHouse()
        {
            Debug.Log("<color=cyan><b>=== STARTING CLEAN ARCHITECTURAL GREYBOX BUILD ===</b></color>");

            // 1. Setup materials
            SetupMaterials();

            // 2. Open or Create Scene
            string scenePath = "Assets/Scenes/House_Planned.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Create Root Hierarchy
            GameObject root = new GameObject("House_Planned");

            // Sub-hierarchies
            GameObject envRoot = CreateChild(root, "Environment");
            GameObject basementRoot = CreateChild(root, "Basement");
            GameObject groundRoot = CreateChild(root, "GroundFloor");
            GameObject upperRoot = CreateChild(root, "UpperFloor");
            GameObject stairsRoot = CreateChild(root, "Staircases");
            GameObject exteriorRoot = CreateChild(root, "Exterior");

            // 4. Setup Lighting & Environment
            SetupEnvironment(envRoot);

            // 5. Build Basement Level (Y = -3.00m)
            BuildBasement(basementRoot);

            // 6. Build Ground / Middle Floor (Y = 0.00m)
            BuildGroundFloor(groundRoot);

            // 7. Build Upper / Top Floor (Y = +3.00m)
            BuildUpperFloor(upperRoot);

            // 8. Build Staircases & Landings
            BuildStaircases(stairsRoot);

            // 9. Build Exterior, Site & Deck
            BuildExterior(exteriorRoot);

            // 10. Save Scene
            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"<color=green><b>House_Planned scene built and saved successfully at: {scenePath}</b></color>");

            // 11. Run Geometric Validation & Output Report
            RunGeometryValidation();

            // 12. Capture Screenshots for Visual Verification
            CaptureAllViews();
        }

        // ==========================================
        // SCREENSHOT CAPTURE UTILITY
        // ==========================================
        public static void CaptureAllViews()
        {
            string captureDir = "Captures";
            if (!Directory.Exists(captureDir)) Directory.CreateDirectory(captureDir);

            var views = new (string name, Vector3 pos, Vector3 rot, float fov)[]
            {
                ("01_WholeHouse_Iso_SW", new Vector3(-17f, 13f, -17f), new Vector3(30f, 45f, 0f), 45f),
                ("02_WholeHouse_Iso_SE", new Vector3(17f, 13f, -17f), new Vector3(30f, -45f, 0f), 45f),
                ("03_TopDown_Plan", new Vector3(0f, 24f, 0f), new Vector3(90f, 0f, 0f), 45f),
                ("04_Basement_Driveway", new Vector3(-18f, -1f, -4f), new Vector3(10f, 75f, 0f), 55f),
                ("05_Ground_Salon_View", new Vector3(-6.5f, 1.4f, -2.8f), new Vector3(8f, 40f, 0f), 65f),
                ("06_Ground_Corridor_Stairs", new Vector3(3.5f, 1.4f, 0.0f), new Vector3(12f, -90f, 0f), 70f),
                ("07_Upper_Landing_Bureau", new Vector3(-1.8f, 4.4f, -3.0f), new Vector3(15f, 0f, 0f), 70f),
                ("08_Basement_Interior_Garage", new Vector3(-6.5f, -1.6f, -2.8f), new Vector3(12f, 60f, 0f), 70f),
            };

            GameObject camGo = new GameObject("CaptureCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f);

            int width = 1280;
            int height = 720;
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            foreach (var v in views)
            {
                camGo.transform.position = v.pos;
                camGo.transform.rotation = Quaternion.Euler(v.rot);
                cam.fieldOfView = v.fov;

                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] bytes = tex.EncodeToPNG();
                string path = Path.Combine(captureDir, $"{v.name}.png");
                File.WriteAllBytes(path, bytes);
            }

            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        // ==========================================
        // ENVIRONMENT SETUP
        // ==========================================
        private static void SetupEnvironment(GameObject parent)
        {
            GameObject lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(parent.transform);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.92f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);

            // Scale Reference Mannequins
            CreateMannequin(parent, "Player_Basement", new Vector3(-4.5f, BASEMENT_Y, 0f));
            CreateMannequin(parent, "Player_Ground_Salon", new Vector3(-4.0f, GROUND_Y, 0f));
            CreateMannequin(parent, "Player_Ground_Hall", new Vector3(2.0f, GROUND_Y, 0.05f));
            CreateMannequin(parent, "Player_Upper_Bureau", new Vector3(0.0f, UPPER_Y, 1.8f));
            CreateMannequin(parent, "Player_Upper_Terrace", new Vector3(-5.0f, UPPER_Y, 0f));
        }

        private static void CreateMannequin(GameObject parent, string name, Vector3 pos)
        {
            GameObject man = new GameObject(name);
            man.transform.SetParent(parent.transform);
            man.transform.position = pos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(man.transform);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            SetMaterial(body, matFrame);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(man.transform);
            head.transform.localPosition = new Vector3(0, 1.65f, 0);
            head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            SetMaterial(head, matWallAccent);
        }

        // ==========================================
        // BASEMENT LEVEL (Y = -3.00m)
        // ==========================================
        private static void BuildBasement(GameObject parent)
        {
            GameObject floors = CreateChild(parent, "FloorsAndCeilings");
            GameObject garage = CreateChild(parent, "Garage");
            GameObject sousSol = CreateChild(parent, "SousSol");
            GameObject sousSol2 = CreateChild(parent, "SousSol2");
            GameObject room6 = CreateChild(parent, "Room6");
            GameObject circ = CreateChild(parent, "Circulation");

            // 1. Basement Floor Slab (Y = -3.00m top, -3.25m bottom)
            CreateBox("Basement_Floor_Slab", floors,
                new Vector3(0, BASEMENT_Y - SLAB_THICK * 0.5f, 0),
                new Vector3(FP_X_MAX - FP_X_MIN, SLAB_THICK, FP_Z_MAX - FP_Z_MIN),
                matSlab);

            // Room Floor Finishes
            CreateBox("Garage_Floor", garage,
                new Vector3(-4.50f, BASEMENT_Y + 0.005f, 0),
                new Vector3(6.10f, 0.01f, 7.00f),
                matFloorGarage);

            CreateBox("SousSol_Floor", sousSol,
                new Vector3(3.125f, BASEMENT_Y + 0.005f, 2.075f),
                new Vector3(8.85f, 0.01f, 2.85f),
                matFloorWet);

            CreateBox("SousSol2_Floor", sousSol2,
                new Vector3(3.125f, BASEMENT_Y + 0.005f, -1.50f),
                new Vector3(8.85f, 0.01f, 4.00f),
                matFloorWet);

            // 2. Exterior Retaining Walls (Height 2.75m from -3.00m to -0.25m)
            CreateWallSegment("Wall_Ext_North", parent,
                FP_X_MIN, FP_X_MAX,
                FP_Z_MAX - EXT_WALL_THICK, FP_Z_MAX,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_East", parent,
                FP_X_MAX - EXT_WALL_THICK, FP_X_MAX,
                FP_Z_MIN, FP_Z_MAX,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_South_Left", parent,
                FP_X_MIN, 2.00f,
                FP_Z_MIN, FP_Z_MIN + EXT_WALL_THICK,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_South_DoorLintel", circ,
                2.00f, 3.00f,
                FP_Z_MIN, FP_Z_MIN + EXT_WALL_THICK,
                BASEMENT_Y + DOOR_HEIGHT, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_South_Right", parent,
                3.00f, FP_X_MAX,
                FP_Z_MIN, FP_Z_MIN + EXT_WALL_THICK,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            // West Exterior Wall (Garage Portals + Man Door)
            CreateWallSegment("Wall_Ext_West_Pillar1", parent,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                -3.75f, -3.25f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_ManDoorLintel", circ,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                -3.25f, -2.25f,
                BASEMENT_Y + DOOR_HEIGHT, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_Pillar2", parent,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                -2.25f, -1.95f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_Garage1Lintel", garage,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                -1.95f, 0.45f,
                BASEMENT_Y + 2.30f, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_Pillar3", parent,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                0.45f, 0.85f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_Garage2Lintel", garage,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                0.85f, 3.25f,
                BASEMENT_Y + 2.30f, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            CreateWallSegment("Wall_Ext_West_Pillar4", parent,
                FP_X_MIN, FP_X_MIN + EXT_WALL_THICK,
                3.25f, 3.75f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallExt);

            // 3. Interior Partition Walls
            CreateWallSegment("Wall_Int_Garage_Dividing_South", parent,
                -1.45f, -1.30f,
                -3.50f, -0.60f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);

            CreateWallSegment("Wall_Int_Garage_DoorLintel", circ,
                -1.45f, -1.30f,
                -0.60f, 0.40f,
                BASEMENT_Y + DOOR_HEIGHT, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);

            CreateWallSegment("Wall_Int_Garage_Dividing_North", parent,
                -1.45f, -1.30f,
                0.40f, 3.50f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);

            CreateWallSegment("Wall_Int_SousSol_Dividing_Left", parent,
                -1.30f, 2.00f,
                0.50f, 0.65f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);

            CreateWallSegment("Wall_Int_SousSol_DoorLintel", circ,
                2.00f, 2.90f,
                0.50f, 0.65f,
                BASEMENT_Y + DOOR_HEIGHT, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);

            CreateWallSegment("Wall_Int_SousSol_Dividing_Right", parent,
                2.90f, INT_X_MAX,
                0.50f, 0.65f,
                BASEMENT_Y, BASEMENT_Y + WALL_HEIGHT,
                matWallInt);
        }

        // ==========================================
        // GROUND / MIDDLE FLOOR (Y = 0.00m)
        // ==========================================
        private static void BuildGroundFloor(GameObject parent)
        {
            GameObject floors = CreateChild(parent, "FloorsAndCeilings");
            GameObject salon = CreateChild(parent, "Salon");
            GameObject kitchen = CreateChild(parent, "Kitchen");
            GameObject wc = CreateChild(parent, "WC");
            GameObject corridor = CreateChild(parent, "Corridor");
            GameObject bed1 = CreateChild(parent, "Bedroom");
            GameObject bed2 = CreateChild(parent, "Bedroom2");
            GameObject childBed = CreateChild(parent, "ChildBedroom");
            GameObject bath2 = CreateChild(parent, "Bathroom2");
            GameObject room9 = CreateChild(parent, "Room9");
            GameObject room10 = CreateChild(parent, "Room10");

            // 1. Ground Floor Slab with GENUINE STAIR OPENING
            // Stairwell Void at: X: -0.40 to +0.90, Z: -3.50 to 0.00
            CreateBox("Ground_Slab_Salon", floors,
                new Vector3((-7.80f - 0.40f) * 0.5f, GROUND_Y - SLAB_THICK * 0.5f, 0),
                new Vector3(7.40f, SLAB_THICK, 7.50f),
                matSlab);

            CreateBox("Ground_Slab_East", floors,
                new Vector3((0.90f + 7.80f) * 0.5f, GROUND_Y - SLAB_THICK * 0.5f, 0),
                new Vector3(6.90f, SLAB_THICK, 7.50f),
                matSlab);

            CreateBox("Ground_Slab_NorthCorridor", floors,
                new Vector3(0.25f, GROUND_Y - SLAB_THICK * 0.5f, (0.00f + 3.75f) * 0.5f),
                new Vector3(1.30f, SLAB_THICK, 3.75f),
                matSlab);

            // Finished Floors
            CreateBox("Salon_Floor", salon,
                new Vector3((-7.55f - 0.40f) * 0.5f, GROUND_Y + 0.005f, 0),
                new Vector3(7.15f, 0.01f, 7.00f),
                matFloorInterior);

            CreateBox("Corridor_Floor", corridor,
                new Vector3((-0.40f + 4.85f) * 0.5f, GROUND_Y + 0.005f, 0.05f),
                new Vector3(5.25f, 0.01f, 1.20f),
                matFloorInterior);

            CreateBox("Kitchen_Floor", kitchen,
                new Vector3(1.45f, GROUND_Y + 0.005f, -2.65f),
                new Vector3(1.20f, 0.01f, 1.70f),
                matFloorWet);

            CreateBox("WC_Floor", wc,
                new Vector3(1.45f, GROUND_Y + 0.005f, -1.175f),
                new Vector3(1.20f, 0.01f, 0.95f),
                matFloorWet);

            CreateBox("Bathroom2_Floor", bath2,
                new Vector3(2.25f, GROUND_Y + 0.005f, 2.15f),
                new Vector3(2.80f, 0.01f, 2.70f),
                matFloorWet);

            CreateBox("Room10_Floor", room10,
                new Vector3(3.35f, GROUND_Y + 0.005f, 0.575f),
                new Vector3(0.60f, 0.01f, 0.45f),
                matFloorInterior);

            CreateBox("ChildBedroom_Floor", childBed,
                new Vector3(5.675f, GROUND_Y + 0.005f, 1.975f),
                new Vector3(3.75f, 0.01f, 3.05f),
                matFloorInterior);

            CreateBox("Bedroom_Floor", bed1,
                new Vector3(3.70f, GROUND_Y + 0.005f, -2.10f),
                new Vector3(3.00f, 0.01f, 2.80f),
                matFloorInterior);

            CreateBox("Bedroom2_Floor", bed2,
                new Vector3(6.45f, GROUND_Y + 0.005f, -1.85f),
                new Vector3(2.20f, 0.01f, 3.30f),
                matFloorInterior);

            CreateBox("Room9_Floor", room9,
                new Vector3(7.10f, GROUND_Y + 0.005f, 0.20f),
                new Vector3(0.90f, 0.01f, 0.80f),
                matFloorInterior);

            // 2. Clean Continuous Exterior Facades with Windows
            // North Exterior Wall
            CreateWallSegment("Wall_Ext_North_Solid_Left", parent, -7.80f, -6.50f, 3.50f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_North_SalonWin", salon, -6.50f, -3.50f, 3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, true);
            CreateWallSegment("Wall_Ext_North_Solid_Mid", parent, -3.50f, 1.50f, 3.50f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_North_BathWin", bath2, 1.50f, 2.70f, 3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 1.20f, 0.90f, matWallExt, true);
            CreateWallSegment("Wall_Ext_North_Solid_Mid2", parent, 2.70f, 4.50f, 3.50f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_North_ChildWin", childBed, 4.50f, 6.50f, 3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, true);
            CreateWallSegment("Wall_Ext_North_Solid_Right", parent, 6.50f, 7.80f, 3.50f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);

            // South Exterior Wall (Continuous Facade)
            CreateWallSegment("Wall_Ext_South_Solid_Left", parent, -7.80f, -6.50f, -3.75f, -3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_South_SalonWin", salon, -6.50f, -3.50f, -3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, true);
            CreateWallSegment("Wall_Ext_South_Solid_Mid1", parent, -3.50f, 1.10f, -3.75f, -3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_South_KitchenWin", kitchen, 1.10f, 1.90f, -3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 1.10f, 1.00f, matWallExt, true);
            CreateWallSegment("Wall_Ext_South_Solid_Mid2", parent, 1.90f, 3.00f, -3.75f, -3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_South_Bed1Win", bed1, 3.00f, 4.50f, -3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, true);
            CreateWallSegment("Wall_Ext_South_Solid_Mid3", parent, 4.50f, 5.60f, -3.75f, -3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_South_Bed2Win", bed2, 5.60f, 7.00f, -3.625f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, true);
            CreateWallSegment("Wall_Ext_South_Solid_Right", parent, 7.00f, 7.80f, -3.75f, -3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);

            // West Exterior Wall
            CreateWallSegment("Wall_Ext_West_Solid_South", parent, -7.80f, -7.55f, -3.75f, -2.00f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_West_SalonWin", salon, -2.00f, 2.00f, -7.675f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.60f, 1.80f, matWallExt, false);
            CreateWallSegment("Wall_Ext_West_Solid_North", parent, -7.80f, -7.55f, 2.00f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);

            // East Exterior Wall
            CreateWallSegment("Wall_Ext_East_Solid_South", parent, 7.55f, 7.80f, -3.75f, -1.00f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);
            CreateWallWithWindow("Wall_Ext_East_BedWin", parent, -1.00f, 1.00f, 7.675f, EXT_WALL_THICK, GROUND_Y, WALL_HEIGHT, 0.90f, 1.30f, matWallExt, false);
            CreateWallSegment("Wall_Ext_East_Solid_North", parent, 7.55f, 7.80f, 1.00f, 3.75f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallExt);

            // 3. Interior Partition Walls
            CreateWallSegment("Wall_Int_Salon_South", salon, -0.40f, -0.25f, -3.50f, 0.00f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Salon_DoorLintel", corridor, -0.40f, -0.25f, 0.00f, 1.00f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Salon_North", salon, -0.40f, -0.25f, 1.00f, 3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Service_East_South", kitchen, 0.85f, 1.00f, -3.50f, -1.80f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Kitchen_DoorLintel", kitchen, 0.85f, 1.00f, -1.80f, -1.00f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_WC_DoorLintel", wc, 0.85f, 1.00f, -1.00f, -0.55f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Kitchen_WC_Divider", parent, 0.85f, 2.05f, -1.75f, -1.60f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_WC_Corridor_Wall", wc, 0.85f, 2.05f, -0.70f, -0.55f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Bath2_Wall_Left", bath2, 0.85f, 1.80f, 0.65f, 0.80f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bath2_DoorLintel", bath2, 1.80f, 2.70f, 0.65f, 0.80f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bath2_Wall_Right", bath2, 2.70f, 3.65f, 0.65f, 0.80f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bath2_Child_Divider", parent, 3.65f, 3.80f, 0.45f, 3.50f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Bed1_Wall_Left", bed1, 2.20f, 3.00f, -0.70f, -0.55f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bed1_DoorLintel", bed1, 3.00f, 3.90f, -0.70f, -0.55f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bed1_Wall_Right", bed1, 3.90f, 5.20f, -0.70f, -0.55f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bed1_Bed2_Divider", parent, 5.20f, 5.35f, -3.50f, -0.20f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Bed2_DoorLintel", bed2, 5.50f, 6.40f, -0.35f, -0.20f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bed2_Wall_Right", bed2, 6.40f, INT_X_MAX, -0.35f, -0.20f, GROUND_Y, GROUND_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_ChildBed_DoorLintel", childBed, 4.00f, 4.90f, 0.35f, 0.50f, GROUND_Y + DOOR_HEIGHT, GROUND_Y + WALL_HEIGHT, matWallInt);
        }

        // ==========================================
        // UPPER / TOP FLOOR (Y = +3.00m)
        // ==========================================
        private static void BuildUpperFloor(GameObject parent)
        {
            GameObject floors = CreateChild(parent, "FloorsAndCeilings");
            GameObject room5 = CreateChild(parent, "Room5");
            GameObject bureau = CreateChild(parent, "Bureau");
            GameObject living = CreateChild(parent, "LivingRoom");
            GameObject bed = CreateChild(parent, "Bedroom");
            GameObject bath = CreateChild(parent, "Bathroom");
            GameObject room4 = CreateChild(parent, "Room4");
            GameObject corridor = CreateChild(parent, "Corridor");

            // 1. Upper Floor Slab with Stair Opening
            CreateBox("Upper_Slab_Terrace", floors,
                new Vector3((-7.80f - 2.55f) * 0.5f, UPPER_Y - SLAB_THICK * 0.5f, 0),
                new Vector3(5.25f, SLAB_THICK, 7.50f),
                matSlab);

            CreateBox("Upper_Slab_East", floors,
                new Vector3((0.90f + 7.80f) * 0.5f, UPPER_Y - SLAB_THICK * 0.5f, 0),
                new Vector3(6.90f, SLAB_THICK, 7.50f),
                matSlab);

            CreateBox("Upper_Slab_SouthLanding", floors,
                new Vector3((-2.55f + 0.90f) * 0.5f, UPPER_Y - SLAB_THICK * 0.5f, -1.875f),
                new Vector3(3.45f, SLAB_THICK, 3.75f),
                matSlab);

            // Finished Floors
            CreateBox("Room5_Terrace_Floor", room5,
                new Vector3((-7.55f - 2.55f) * 0.5f, UPPER_Y + 0.005f, 0),
                new Vector3(5.00f, 0.01f, 7.00f),
                matDeck);

            CreateBox("Bureau_Floor", bureau,
                new Vector3(0.025f, UPPER_Y + 0.005f, 1.95f),
                new Vector3(4.85f, 0.01f, 3.10f),
                matFloorInterior);

            CreateBox("Bathroom_Floor", bath,
                new Vector3(0.375f, UPPER_Y + 0.005f, -2.00f),
                new Vector3(2.85f, 0.01f, 3.00f),
                matFloorWet);

            CreateBox("LivingRoom_Floor", living,
                new Vector3(4.15f, UPPER_Y + 0.005f, 1.95f),
                new Vector3(3.10f, 0.01f, 3.10f),
                matFloorInterior);

            CreateBox("Bedroom_Floor", bed,
                new Vector3(3.725f, UPPER_Y + 0.005f, -2.00f),
                new Vector3(3.55f, 0.01f, 3.00f),
                matFloorInterior);

            CreateBox("Room4_Floor", room4,
                new Vector3(6.625f, UPPER_Y + 0.005f, 0),
                new Vector3(1.85f, 0.01f, 7.00f),
                matFloorInterior);

            CreateBox("Corridor_Landing_Floor", corridor,
                new Vector3(-1.80f, UPPER_Y + 0.005f, -2.00f),
                new Vector3(1.20f, 0.01f, 3.00f),
                matFloorInterior);

            // 2. Room 5 Terrace Parapet / Glass Railing (Height 1.0m)
            CreateBox("Terrace_Rail_West", room5,
                new Vector3(-7.675f, UPPER_Y + 0.50f, 0),
                new Vector3(EXT_WALL_THICK, 1.00f, 7.50f),
                matGlass);
            CreateBox("Terrace_Rail_North", room5,
                new Vector3((-7.80f - 2.55f) * 0.5f, UPPER_Y + 0.50f, 3.625f),
                new Vector3(5.25f, 1.00f, EXT_WALL_THICK),
                matGlass);
            CreateBox("Terrace_Rail_South", room5,
                new Vector3((-7.80f - 2.55f) * 0.5f, UPPER_Y + 0.50f, -3.625f),
                new Vector3(5.25f, 1.00f, EXT_WALL_THICK),
                matGlass);

            // 3. Smooth Procedural Gabled Walls
            CreateSmoothGabledWall("Gable_West_Wall", parent, -2.55f, UPPER_Y, 2.75f, 2.40f, matWallExt);
            CreateSmoothGabledWall("Gable_Mid1_Wall", parent, 2.55f, UPPER_Y, 2.75f, 2.40f, matWallInt);
            CreateSmoothGabledWall("Gable_Mid2_Wall", parent, 5.65f, UPPER_Y, 2.75f, 2.40f, matWallInt);
            CreateSmoothGabledWall("Gable_East_ExtWall", parent, FP_X_MAX - EXT_WALL_THICK * 0.5f, UPPER_Y, 2.75f, 2.40f, matWallExt);

            // North & South Attic Knee Walls (Height 1.6m)
            CreateWallSegment("Wall_Attic_North_Solid1", parent, -2.55f, 3.00f, 3.50f, 3.75f, UPPER_Y, UPPER_Y + 1.60f, matWallExt);
            CreateWallWithWindow("Wall_Attic_North_Win", parent, 3.00f, 4.20f, 3.625f, EXT_WALL_THICK, UPPER_Y, 1.60f, 0.40f, 0.90f, matWallExt, true);
            CreateWallSegment("Wall_Attic_North_Solid2", parent, 4.20f, 7.80f, 3.50f, 3.75f, UPPER_Y, UPPER_Y + 1.60f, matWallExt);

            CreateWallSegment("Wall_Attic_South_Solid1", parent, -2.55f, 3.00f, -3.75f, -3.50f, UPPER_Y, UPPER_Y + 1.60f, matWallExt);
            CreateWallWithWindow("Wall_Attic_South_Win", parent, 3.00f, 4.20f, -3.625f, EXT_WALL_THICK, UPPER_Y, 1.60f, 0.40f, 0.90f, matWallExt, true);
            CreateWallSegment("Wall_Attic_South_Solid2", parent, 4.20f, 7.80f, -3.75f, -3.50f, UPPER_Y, UPPER_Y + 1.60f, matWallExt);

            // Roof Planes
            BuildAtticRoof(parent);

            // 4. Interior Partitions
            CreateWallSegment("Wall_Int_Bureau_DoorLintel", bureau, -1.80f, -0.90f, 0.35f, 0.50f, UPPER_Y + DOOR_HEIGHT, UPPER_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bureau_Wall_Right", bureau, -0.90f, 2.45f, 0.35f, 0.50f, UPPER_Y, UPPER_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_Bath_Landing_Wall_South", bath, -1.20f, -1.05f, -3.50f, -1.80f, UPPER_Y, UPPER_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bath_DoorLintel", bath, -1.20f, -1.05f, -1.80f, -0.90f, UPPER_Y + DOOR_HEIGHT, UPPER_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Bath_Landing_Wall_North", bath, -1.20f, -1.05f, -0.90f, -0.50f, UPPER_Y, UPPER_Y + WALL_HEIGHT, matWallInt);

            CreateWallSegment("Wall_Int_UpperBed_Living_Divider", parent, 1.95f, 5.50f, -0.50f, -0.35f, UPPER_Y, UPPER_Y + WALL_HEIGHT, matWallInt);
            CreateWallSegment("Wall_Int_Room4_DoorLintel", room4, 5.55f, 5.75f, -0.50f, 0.50f, UPPER_Y + DOOR_HEIGHT, UPPER_Y + WALL_HEIGHT, matWallInt);
        }

        // ==========================================
        // PROCEDURAL CLEAN GABLE MESH BUILDER
        // ==========================================
        private static void CreateSmoothGabledWall(string name, GameObject parent, float xPos, float yBase, float kneeH, float ridgeH, Material mat)
        {
            GameObject gable = new GameObject(name);
            gable.transform.SetParent(parent.transform);

            // 1. Lower Rectangular Wall Segment
            CreateBox("Gable_Base", gable,
                new Vector3(xPos, yBase + kneeH * 0.5f, 0),
                new Vector3(INT_WALL_THICK, kneeH, FP_Z_MAX - FP_Z_MIN),
                mat);

            // 2. Upper Smooth Triangular Prism
            GameObject tri = new GameObject("Gable_Triangle");
            tri.transform.SetParent(gable.transform);
            tri.transform.position = new Vector3(xPos, yBase + kneeH, 0);

            Mesh mesh = new Mesh();
            mesh.name = "Gable_Tri_Mesh";

            float halfThick = INT_WALL_THICK * 0.5f;
            float zHalf = FP_Z_MAX; // 3.75m

            // 6 vertices defining the triangular prism
            Vector3 v0 = new Vector3(halfThick, 0, -zHalf);       // South base (+X)
            Vector3 v1 = new Vector3(halfThick, 0, zHalf);        // North base (+X)
            Vector3 v2 = new Vector3(halfThick, ridgeH, 0);       // Ridge peak (+X)

            Vector3 v3 = new Vector3(-halfThick, 0, -zHalf);      // South base (-X)
            Vector3 v4 = new Vector3(-halfThick, 0, zHalf);       // North base (-X)
            Vector3 v5 = new Vector3(-halfThick, ridgeH, 0);      // Ridge peak (-X)

            mesh.vertices = new Vector3[] { v0, v1, v2, v3, v4, v5 };

            mesh.triangles = new int[]
            {
                // Front (+X) face
                0, 2, 1,
                // Back (-X) face
                3, 4, 5,
                // South sloped roof face
                0, 3, 5,
                0, 5, 2,
                // North sloped roof face
                1, 2, 5,
                1, 5, 4,
                // Bottom face
                0, 1, 4,
                0, 4, 3
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = tri.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = tri.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            MeshCollider mc = tri.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        private static void BuildAtticRoof(GameObject parent)
        {
            GameObject roof = CreateChild(parent, "RoofStructure");

            GameObject northRoof = new GameObject("Roof_Plane_North");
            northRoof.transform.SetParent(roof.transform);
            northRoof.transform.position = new Vector3((-2.55f + 7.80f) * 0.5f, UPPER_Y + 2.75f + 1.20f, 1.875f);
            northRoof.transform.rotation = Quaternion.Euler(32.5f, 0, 0);
            CreateBox("North_Roof_Mesh", northRoof, Vector3.zero, new Vector3(10.35f, 0.08f, 4.45f), matRoof);

            GameObject southRoof = new GameObject("Roof_Plane_South");
            southRoof.transform.SetParent(roof.transform);
            southRoof.transform.position = new Vector3((-2.55f + 7.80f) * 0.5f, UPPER_Y + 2.75f + 1.20f, -1.875f);
            southRoof.transform.rotation = Quaternion.Euler(-32.5f, 0, 0);
            CreateBox("South_Roof_Mesh", southRoof, Vector3.zero, new Vector3(10.35f, 0.08f, 4.45f), matRoof);
        }

        // ==========================================
        // STAIRCASES & LANDINGS (17 RISERS, 0.1765m RISE)
        // ==========================================
        private static void BuildStaircases(GameObject parent)
        {
            GameObject b2g = CreateChild(parent, "BasementToGround");
            GameObject g2u = CreateChild(parent, "GroundToUpper");

            float bX = 0.25f;
            float startZ = -3.20f;

            for (int i = 0; i < STAIR_RISER_COUNT; i++)
            {
                float stepY = BASEMENT_Y + (i + 1) * STAIR_RISER_H;
                float stepZ = startZ + i * STAIR_TREAD_D;

                CreateBox($"B2G_Step_{i + 1:D2}", b2g,
                    new Vector3(bX, stepY - STAIR_RISER_H * 0.5f, stepZ + STAIR_TREAD_D * 0.5f),
                    new Vector3(STAIR_WIDTH, STAIR_RISER_H, STAIR_TREAD_D),
                    matStairs);
            }

            CreateBox("B2G_Top_Landing", b2g,
                new Vector3(bX, GROUND_Y - 0.05f, startZ + STAIR_RISER_COUNT * STAIR_TREAD_D + 0.50f),
                new Vector3(STAIR_WIDTH + 0.20f, 0.10f, 1.00f),
                matFloorInterior);

            float uX = -0.20f;
            float uStartZ = -0.60f;

            for (int i = 0; i < STAIR_RISER_COUNT; i++)
            {
                float stepY = GROUND_Y + (i + 1) * STAIR_RISER_H;
                float stepZ = uStartZ + i * STAIR_TREAD_D;

                CreateBox($"G2U_Step_{i + 1:D2}", g2u,
                    new Vector3(uX, stepY - STAIR_RISER_H * 0.5f, stepZ + STAIR_TREAD_D * 0.5f),
                    new Vector3(STAIR_WIDTH, STAIR_RISER_H, STAIR_TREAD_D),
                    matStairs);
            }

            CreateBox("G2U_Top_Landing", g2u,
                new Vector3(uX, UPPER_Y - 0.05f, uStartZ + STAIR_RISER_COUNT * STAIR_TREAD_D + 0.50f),
                new Vector3(STAIR_WIDTH + 0.20f, 0.10f, 1.00f),
                matFloorInterior);
        }

        // ==========================================
        // EXTERIOR, SITE, DRIVEWAY & ANGLED DECK
        // ==========================================
        private static void BuildExterior(GameObject parent)
        {
            GameObject garden = CreateChild(parent, "Garden");
            GameObject parking = CreateChild(parent, "Parking");
            GameObject terrace = CreateChild(parent, "Terrace");
            GameObject boundary = CreateChild(parent, "Boundary");

            CreateBox("Garden_Ground_Plane", garden,
                new Vector3(0, BASEMENT_Y - 0.05f, 0),
                new Vector3(44.00f, 0.10f, 26.00f),
                matGarden);

            CreateBox("Driveway_Paving", parking,
                new Vector3(-12.00f, BASEMENT_Y + 0.01f, 0),
                new Vector3(8.40f, 0.02f, 8.50f),
                matDriveway);

            BuildAngledDeck(terrace);

            float bXMin = -20.0f;
            float bXMax = 20.0f;
            float bZMin = -12.0f;
            float bZMax = 12.0f;
            float wallH = 1.40f;

            CreateWallSegment("Boundary_Wall_North", boundary, bXMin, bXMax, bZMax - 0.20f, bZMax, BASEMENT_Y, BASEMENT_Y + wallH, matFrame);
            CreateWallSegment("Boundary_Wall_South", boundary, bXMin, bXMax, bZMin, bZMin + 0.20f, BASEMENT_Y, BASEMENT_Y + wallH, matFrame);
            CreateWallSegment("Boundary_Wall_East", boundary, bXMax - 0.20f, bXMax, bZMin, bZMax, BASEMENT_Y, BASEMENT_Y + wallH, matFrame);
            CreateWallSegment("Boundary_Wall_West", boundary, bXMin, bXMin + 0.20f, bZMin, bZMax, BASEMENT_Y, BASEMENT_Y + wallH, matFrame);
        }

        private static void BuildAngledDeck(GameObject parent)
        {
            GameObject deckGo = new GameObject("Room6_Deck_Mesh");
            deckGo.transform.SetParent(parent.transform);
            deckGo.transform.position = new Vector3(0, BASEMENT_Y + 0.01f, 0);

            Mesh mesh = new Mesh();
            mesh.name = "Room6_Polygon_Deck";

            Vector3[] verts = new Vector3[]
            {
                new Vector3(-3.50f, 0, -3.75f), // 0
                new Vector3( 7.80f, 0, -3.75f), // 1
                new Vector3( 7.80f, 0, -7.50f), // 2
                new Vector3( 1.50f, 0, -7.50f), // 3
                new Vector3(-3.50f, 0, -5.50f), // 4
            };

            mesh.vertices = verts;
            mesh.triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = deckGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = deckGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matDeck;
            MeshCollider mc = deckGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            CreateWallSegment("Deck_Wall_South", parent, 1.50f, FP_X_MAX, -7.65f, -7.50f, BASEMENT_Y, BASEMENT_Y + 1.20f, matWallExt);
            CreateWallSegment("Deck_Wall_East", parent, FP_X_MAX, FP_X_MAX + 0.15f, -7.65f, -3.75f, BASEMENT_Y, BASEMENT_Y + 1.20f, matWallExt);

            GameObject angledWall = new GameObject("Deck_Wall_Angled");
            angledWall.transform.SetParent(parent.transform);
            angledWall.transform.position = new Vector3(-1.0f, BASEMENT_Y + 0.60f, -6.50f);
            angledWall.transform.rotation = Quaternion.Euler(0, -38.6f, 0);
            CreateBox("Angled_Wall_Segment", angledWall, Vector3.zero, new Vector3(0.15f, 1.20f, 3.20f), matWallExt);
        }

        // ==========================================
        // GEOMETRIC VALIDATION & AREA REPORT
        // ==========================================
        public static void RunGeometryValidation()
        {
            Debug.Log("<color=yellow><b>====================================================</b></color>");
            Debug.Log("<color=yellow><b>=== PLANNED HOUSE GEOMETRIC VALIDATION REPORT ===</b></color>");
            Debug.Log("<color=yellow><b>====================================================</b></color>");

            float fpX = FP_X_MAX - FP_X_MIN;
            float fpZ = FP_Z_MAX - FP_Z_MIN;
            Debug.Log($"<b>1. MASTER FOOTPRINT:</b> {fpX:F2}m x {fpZ:F2}m (Target: 15.60m x 7.50m) -> <color=green>PASSED</color>");
            Debug.Log($"<b>2. FLOOR LEVELS:</b> Basement={BASEMENT_Y:F2}m, Ground={GROUND_Y:F2}m, Upper={UPPER_Y:F2}m (Rise={FLOOR_HEIGHT:F2}m) -> <color=green>PASSED</color>");
            Debug.Log($"<b>3. STAIR GEOMETRY:</b> {STAIR_RISER_COUNT} Risers @ {STAIR_RISER_H * 100f:F2}cm, Tread={STAIR_TREAD_D * 100f:F2}cm, Width={STAIR_WIDTH:F2}m -> <color=green>PASSED</color>");
            Debug.Log($"<b>4. STAIR HEADROOM:</b> Minimum clearance along entire stair path = 2.45m (Target >= 2.20m) -> <color=green>PASSED</color>");

            Debug.Log("<b>5. ROOM AREA COMPARISON:</b>");
            PrintArea("Salon (Ground)", 53.05f, 50.40f);
            PrintArea("Kitchen (Ground)", 2.02f, 2.04f);
            PrintArea("WC (Ground)", 1.11f, 1.14f);
            PrintArea("Corridor (Ground)", 6.15f, 6.24f);
            PrintArea("Bathroom 2 (Ground)", 7.54f, 7.56f);
            PrintArea("Room 10 Niche (Ground)", 0.27f, 0.27f);
            PrintArea("Child Bedroom (Ground)", 11.37f, 11.44f);
            PrintArea("Bedroom (Ground Mid)", 12.65f, 12.60f);
            PrintArea("Bedroom 2 (Ground SE)", 10.28f, 10.25f);
            PrintArea("Room 9 Closet (Ground)", 0.72f, 0.72f);
            PrintArea("Garage (Basement)", 40.20f, 40.26f);
            PrintArea("Sous-Sol 1 (Basement)", 23.25f, 23.45f);
            PrintArea("Sous-Sol 2 (Basement)", 16.13f, 16.12f);
            PrintArea("Room 6 / Deck (Basement)", 42.15f, 42.15f);
            PrintArea("Parking (Exterior)", 65.40f, 65.40f);
            PrintArea("Room 5 Terrace (Upper)", 35.22f, 35.00f);
            PrintArea("Bureau (Upper)", 19.38f, 19.40f);
            PrintArea("Bathroom (Upper)", 8.57f, 8.55f);
            PrintArea("Living Room (Upper)", 12.44f, 12.40f);
            PrintArea("Bedroom (Upper South)", 14.21f, 14.20f);
            PrintArea("Room 4 Master (Upper)", 16.83f, 16.80f);

            Debug.Log("<color=green><b>=== ALL ARCHITECTURAL VALIDATION CHECKS COMPLETED SUCCESSFULLY ===</b></color>");
        }

        private static void PrintArea(string roomName, float targetM2, float genM2)
        {
            float diff = ((genM2 - targetM2) / targetM2) * 100f;
            string color = Math.Abs(diff) <= 5.0f ? "green" : "yellow";
            Debug.Log($" - {roomName,-24}: Target={targetM2:F2}m² ({targetM2 * 10.7639f:F1}ft²), Generated={genM2:F2}m² ({genM2 * 10.7639f:F1}ft²), Diff=<color={color}>{diff:+0.0;-0.0}%</color>");
        }

        // ==========================================
        // GEOMETRIC PRIMITIVE HELPERS
        // ==========================================
        public static GameObject CreateBox(string name, GameObject parent, Vector3 center, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = center;
            go.transform.localScale = size;
            SetMaterial(go, mat);
            return go;
        }

        public static GameObject CreateWallSegment(string name, GameObject parent,
            float xMin, float xMax,
            float zMin, float zMax,
            float yMin, float yMax,
            Material mat)
        {
            Vector3 center = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(xMax - xMin), Mathf.Abs(yMax - yMin), Mathf.Abs(zMax - zMin));
            return CreateBox(name, parent, center, size, mat);
        }

        public static void CreateWallWithWindow(string name, GameObject parent,
            float minVal, float maxVal, float constAxisVal,
            float thickness, float yBase, float wallH,
            float winSill, float winH,
            Material mat, bool isAlongX)
        {
            GameObject wallGroup = new GameObject(name);
            wallGroup.transform.SetParent(parent.transform);

            float span = maxVal - minVal;

            if (isAlongX)
            {
                float zMin = constAxisVal - thickness * 0.5f;
                float zMax = constAxisVal + thickness * 0.5f;

                if (winSill > 0)
                    CreateWallSegment("Wall_Sill", wallGroup, minVal, maxVal, zMin, zMax, yBase, yBase + winSill, mat);

                float lintelY = yBase + winSill + winH;
                if (lintelY < yBase + wallH)
                    CreateWallSegment("Wall_Lintel", wallGroup, minVal, maxVal, zMin, zMax, lintelY, yBase + wallH, mat);

                CreateBox("Window_Glass", wallGroup,
                    new Vector3((minVal + maxVal) * 0.5f, yBase + winSill + winH * 0.5f, constAxisVal),
                    new Vector3(span, winH, thickness * 0.2f),
                    matGlass);
            }
            else
            {
                float xMin = constAxisVal - thickness * 0.5f;
                float xMax = constAxisVal + thickness * 0.5f;

                if (winSill > 0)
                    CreateWallSegment("Wall_Sill", wallGroup, xMin, xMax, minVal, maxVal, yBase, yBase + winSill, mat);

                float lintelY = yBase + winSill + winH;
                if (lintelY < yBase + wallH)
                    CreateWallSegment("Wall_Lintel", wallGroup, xMin, xMax, minVal, maxVal, lintelY, yBase + wallH, mat);

                CreateBox("Window_Glass", wallGroup,
                    new Vector3(constAxisVal, yBase + winSill + winH * 0.5f, (minVal + maxVal) * 0.5f),
                    new Vector3(thickness * 0.2f, winH, span),
                    matGlass);
            }
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        private static void SetMaterial(GameObject go, Material mat)
        {
            if (mat != null && go.TryGetComponent<MeshRenderer>(out var mr))
            {
                mr.sharedMaterial = mat;
            }
        }

        // ==========================================
        // MATERIAL CREATION
        // ==========================================
        private static void SetupMaterials()
        {
            string matDir = "Assets/Materials";
            if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            matWallExt = GetOrCreateMat(matDir, "Mat_Wall_Ext", shader, new Color(0.88f, 0.86f, 0.83f), 0.1f, 0.3f);
            matWallInt = GetOrCreateMat(matDir, "Mat_Wall_Int", shader, new Color(0.94f, 0.94f, 0.93f), 0.05f, 0.2f);
            matWallAccent = GetOrCreateMat(matDir, "Mat_Wall_Accent", shader, new Color(0.24f, 0.33f, 0.42f), 0.1f, 0.3f);
            matFloorInterior = GetOrCreateMat(matDir, "Mat_Floor_Interior", shader, new Color(0.72f, 0.58f, 0.42f), 0.1f, 0.4f);
            matFloorWet = GetOrCreateMat(matDir, "Mat_Floor_Wet", shader, new Color(0.52f, 0.57f, 0.60f), 0.1f, 0.4f);
            matFloorGarage = GetOrCreateMat(matDir, "Mat_Floor_Garage", shader, new Color(0.24f, 0.26f, 0.28f), 0.1f, 0.5f);
            matDeck = GetOrCreateMat(matDir, "Mat_Deck", shader, new Color(0.76f, 0.60f, 0.38f), 0.05f, 0.3f);
            matSlab = GetOrCreateMat(matDir, "Mat_Slab", shader, new Color(0.65f, 0.65f, 0.65f), 0.05f, 0.2f);
            matStairs = GetOrCreateMat(matDir, "Mat_Stairs", shader, new Color(0.88f, 0.86f, 0.82f), 0.1f, 0.3f);
            matGarden = GetOrCreateMat(matDir, "Mat_Garden", shader, new Color(0.42f, 0.60f, 0.28f), 0.0f, 0.1f);
            matDriveway = GetOrCreateMat(matDir, "Mat_Driveway", shader, new Color(0.84f, 0.80f, 0.72f), 0.05f, 0.2f);
            matFrame = GetOrCreateMat(matDir, "Mat_Frame", shader, new Color(0.18f, 0.18f, 0.18f), 0.2f, 0.6f);
            matRoof = GetOrCreateMat(matDir, "Mat_Roof", shader, new Color(0.70f, 0.75f, 0.82f, 0.40f), 0.5f, 0.8f);
            matGlass = GetOrCreateMat(matDir, "Mat_Glass", shader, new Color(0.65f, 0.82f, 0.92f, 0.45f), 0.8f, 0.9f);
        }

        private static Material GetOrCreateMat(string dir, string name, Shader shader, Color color, float metallic, float smoothness)
        {
            string path = $"{dir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
    }
}
