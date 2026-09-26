using System;
using System.Collections.Generic;
using System.IO;
using IdleRestaurant.Ads;
using IdleRestaurant.Audio;
using IdleRestaurant.Core;
using IdleRestaurant.Data;
using IdleRestaurant.Gameplay;
using IdleRestaurant.Gameplay.Customers;
using IdleRestaurant.Gameplay.Quests;
using IdleRestaurant.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleRestaurant.EditorTools
{
    /// <summary>
    /// Tek tıkla oynanabilir 3D prototip sahnesi üretir:
    /// Tools → Idle Restaurant → Generate Complete 3D Prototype Scene.
    ///
    /// Ürettikleri:
    /// <list type="bullet">
    /// <item>Assets/GameData altında pastel malzemeler, 3 StationData, SoundLibrary, 4 başlangıç görevi ve
    ///       CustomerPrefab (CustomerVisual ile zıplayıp yalpalayan, gözlü sevimli müşteri)</item>
    /// <item>Assets/Scenes/MainGame.unity: izometrik ortografik kamera, sıcak ışık, açık ahşap parke zeminli,
    ///       nane yeşili/krem duvarlı restoran, üzerinde tabak/bardak olan 3 istasyon, kapılar, müşteri akışı,
    ///       tüm yöneticiler ve 1080x1920 dikey arayüz</item>
    /// <item>Sahneyi Build Settings'in başına ekler ve varsayılan yönü dikey yapar</item>
    /// </list>
    ///
    /// ── Referanslar ─────────────────────────────────────────────────────────
    /// Tüm Inspector alanları SerializedObject ile, alan adlarıyla bağlanır.
    /// Bir bileşende alan adı değişirse araç sessizce eksik sahne üretmez;
    /// hangi alanın bulunamadığını söyleyerek durur.
    ///
    /// ── Asset'ler ───────────────────────────────────────────────────────────
    /// Sahneye bağlanan her asset (StationData, SoundLibrary, görevler,
    /// prefab, malzemeler) önce diske yazılır, SaveAssets + Refresh yapılır,
    /// sonra diskteki yolundan yeniden yüklenir. Sahne yalnızca diskte
    /// karşılığı olan nesnelere bağlanır; bellekte kalmış bir kopyaya değil.
    ///
    /// ── Tekrar çalıştırma ───────────────────────────────────────────────────
    /// Sahne her seferinde baştan üretilir (üzerine yazılır). Assets/GameData
    /// altındaki asset'ler ise VARSA KORUNUR: tasarımcının değiştirdiği
    /// fiyatlar, görevler ve prefab ikinci çalıştırmada ezilmez. Beklenen
    /// yolda yüklenemeyen bir dosya (script'i kayıp, başka tipte) varsa çöp
    /// kutusuna taşınıp yeniden üretilir. Sıfırdan üretmek için
    /// Assets/GameData klasörünü silin.
    /// </summary>
    public static class SceneSetupTool
    {
        public const string MenuPath = "Tools/Idle Restaurant/Generate Complete 3D Prototype Scene";
        public const string ScenePath = "Assets/Scenes/MainGame.unity";
        public const string DataRoot = "Assets/GameData";

        private const string StationsFolder = DataRoot + "/Stations";
        private const string QuestsFolder = DataRoot + "/Quests";
        private const string AudioFolder = DataRoot + "/Audio";
        /// <summary>
        /// Malzemeler tema klasöründe: palet değişince yeni klasör açılır,
        /// böylece var olan (korunan) malzemeler yeni renkleri engellemez.
        /// Eski temanın malzemeleri dokunulmadan kalır; kullanılmıyorsa silinebilir.
        /// </summary>
        private const string ThemeName = "Pastel";
        private const string MaterialsRoot = DataRoot + "/Materials";
        private const string MaterialsFolder = MaterialsRoot + "/" + ThemeName;
        private const string PrefabsFolder = DataRoot + "/Prefabs";
        private const string CustomerPrefabPath = PrefabsFolder + "/CustomerPrefab.prefab";
        private const string SoundLibraryPath = AudioFolder + "/SoundLibrary.asset";
        private const string TmpImportMenu = "Window/TextMeshPro/Import TMP Essential Resources";

        // Kamera (istenen değerler) ve restoranın ekrandaki yeri.
        private static readonly Vector3 CameraPosition = new Vector3(-8f, 12f, -8f);
        private static readonly Vector3 CameraEuler = new Vector3(35f, 45f, 0f);
        private const float CameraSize = 8f;

        /// <summary>
        /// Restoran ekranın bu yüksekliğine (0 = alt, 1 = üst) ortalanır.
        /// Merkezin biraz üstü: alttaki istasyon satırları sahnenin önünü kapatmasın.
        /// </summary>
        private const float ViewportFocusY = 0.6f;

        private const float FloorSize = 6f;

        /// <summary>Tezgah üst yüzeyinin yüksekliği; servis takımı bunun üstüne konur.</summary>
        private const float CounterTopHeight = 0.98f;

        private const float PlankWidth = 0.5f;
        private const float PlankLength = 1.5f;
        private const float PlankGap = 0.03f;

        /// <summary>Parke satırlarının ek yeri kaydırmaları; dört satırda bir tekrarlar.</summary>
        private static readonly float[] PlankStagger = { 0f, 0.75f, 0.375f, 1.125f };
        private const float CanvasWidth = 1080f;
        private const float CanvasHeight = 1920f;

        private static readonly StationSpec[] Stations =
        {
            new StationSpec("burger_counter", "Burger Tezgahı", 10d, 2d, 1.5f, 1,
                new Color(0.98f, 0.71f, 0.58f), new Color(0.93f, 0.66f, 0.36f), new Color(0.96f, 0.56f, 0.55f)),
            new StationSpec("coffee_bar", "Kahve Barı", 50d, 8d, 2.0f, 0,
                new Color(0.84f, 0.71f, 0.6f), new Color(0.55f, 0.38f, 0.28f), new Color(0.45f, 0.3f, 0.2f)),
            new StationSpec("pizza_oven", "Pizza Fırını", 200d, 25d, 3.5f, 0,
                new Color(0.66f, 0.84f, 0.69f), new Color(0.9f, 0.55f, 0.44f), new Color(0.99f, 0.88f, 0.52f))
        };

        /// <summary>
        /// Başlangıç görev zinciri: para kazan → 5 kez yükselt → daha çok para
        /// kazan → ilk prestij. Zincir tekrarlandıkça hedefler büyür.
        /// </summary>
        private static readonly QuestSpec[] QuestChain =
        {
            new QuestSpec("q01_earn_100", ScriptableObject.CreateInstance<EarnMoneyQuestDefinition>, 100d, 3d, 50d, 3d, 0f),
            new QuestSpec("q02_upgrade_5", ScriptableObject.CreateInstance<UpgradeStationQuestDefinition>, 5d, 1.5d, 100d, 2d, 10f),
            new QuestSpec("q03_earn_1000", ScriptableObject.CreateInstance<EarnMoneyQuestDefinition>, 1000d, 3d, 300d, 3d, 20f),
            new QuestSpec("q04_first_prestige", ScriptableObject.CreateInstance<PrestigeQuestDefinition>, 1d, 1d, 0d, 1d, 120f)
        };

        /// <summary>
        /// Sıcak, modern pastel palet: açık ahşap parke, nane yeşili + krem
        /// duvarlar, beyaz pervazlar, şeftali halı. İstasyon malzemeleri
        /// <see cref="Stations"/>'tan gelir.
        /// </summary>
        private static readonly MaterialSpec[] SharedMaterials =
        {
            new MaterialSpec(MaterialName.FloorBase, new Color(0.66f, 0.52f, 0.4f)),
            new MaterialSpec(MaterialName.WoodLight, new Color(0.93f, 0.8f, 0.62f), 0.3f),
            new MaterialSpec(MaterialName.WoodHoney, new Color(0.89f, 0.74f, 0.54f), 0.3f),
            new MaterialSpec(MaterialName.WoodPale, new Color(0.96f, 0.86f, 0.7f), 0.3f),
            new MaterialSpec(MaterialName.WallMint, new Color(0.69f, 0.88f, 0.8f)),
            new MaterialSpec(MaterialName.WallCream, new Color(0.99f, 0.95f, 0.87f)),
            new MaterialSpec(MaterialName.Trim, new Color(1f, 0.99f, 0.96f), 0.35f),
            new MaterialSpec(MaterialName.Rug, new Color(0.98f, 0.73f, 0.64f)),
            new MaterialSpec(MaterialName.RugBorder, new Color(0.99f, 0.93f, 0.84f)),
            new MaterialSpec(MaterialName.Wood, new Color(0.8f, 0.63f, 0.46f), 0.25f),
            new MaterialSpec(MaterialName.CounterTop, new Color(0.97f, 0.96f, 0.93f), 0.55f),
            new MaterialSpec(MaterialName.EntryMat, new Color(0.56f, 0.82f, 0.69f)),
            new MaterialSpec(MaterialName.ExitMat, new Color(0.95f, 0.62f, 0.58f)),
            new MaterialSpec(MaterialName.Plant, new Color(0.53f, 0.75f, 0.56f)),
            new MaterialSpec(MaterialName.Pot, new Color(0.91f, 0.64f, 0.5f)),
            new MaterialSpec(MaterialName.Spot, new Color(1f, 0.88f, 0.56f)),
            new MaterialSpec(MaterialName.Ceramic, new Color(1f, 0.99f, 0.97f), 0.6f),
            new MaterialSpec(MaterialName.Napkin, new Color(0.74f, 0.9f, 0.84f)),
            new MaterialSpec(MaterialName.Cutlery, new Color(0.82f, 0.83f, 0.86f), 0.7f),
            new MaterialSpec(MaterialName.Customer, new Color(0.56f, 0.73f, 0.96f), 0.25f),
            new MaterialSpec(MaterialName.CustomerHat, new Color(0.99f, 0.8f, 0.84f)),
            new MaterialSpec(MaterialName.Eye, new Color(0.16f, 0.13f, 0.13f), 0.6f),
            new MaterialSpec(MaterialName.Blush, new Color(1f, 0.64f, 0.68f))
        };

        private static readonly Color BackgroundColor = new Color(0.98f, 0.88f, 0.79f);

        // ── Menü ───────────────────────────────────────────────────────────────

        [MenuItem(MenuPath, false, 0)]
        public static void GenerateFromMenu()
        {
            if (!EnsureTextMeshProResources())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog("Idle Restaurant",
                    $"{ScenePath} zaten var ve baştan üretilecek. Sahnede elle yaptığınız değişiklikler kaybolur.\n\n" +
                    "Assets/GameData altındaki asset'ler korunur.", "Üret", "İptal"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Idle Restaurant", "Prototip sahnesi üretiliyor...", 0.5f);
                SceneSetupResult result = Generate();
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Idle Restaurant",
                    $"Sahne hazır: {result.ScenePath}\n\n" +
                    $"Yeni asset: {result.CreatedAssets}, korunan asset: {result.ReusedAssets}\n\n" +
                    "Play'e basarak oynayabilirsiniz. Yayından önce: SettingsPanelUI → Privacy Policy Url.", "Tamam");
            }
            catch (Exception exception)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Idle Restaurant", "Sahne üretilemedi:\n" + exception.Message, "Tamam");
            }
        }

        /// <summary>
        /// TextMeshPro'nun temel kaynakları (varsayılan font) projede yoksa
        /// metinler görünmez. Unity bunları ilk kullanımda bir kez ister;
        /// eksikse içe aktarma penceresini açıp üretimi durdurur.
        /// </summary>
        private static bool EnsureTextMeshProResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return true;
            }

            if (EditorUtility.DisplayDialog("TextMeshPro kaynakları eksik",
                    "Arayüz metinleri için TextMeshPro'nun temel kaynakları gerekiyor.\n\n" +
                    "Açılacak pencerede 'Import TMP Essentials'a basın, sonra bu menüyü yeniden çalıştırın.",
                    "Pencereyi Aç", "İptal"))
            {
                EditorApplication.ExecuteMenuItem(TmpImportMenu);
            }

            return false;
        }

        // ── Üretim ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Asıl üretim; menüden bağımsızdır (diyalog göstermez), otomasyonla
        /// da çağrılabilir. Hata durumunda istisna atar.
        ///
        /// Sıra bilinçli:
        /// <list type="number">
        /// <item>Önce boş sahne açılır. NewScene(Single) açık sahnenin
        ///       kullanmadığı asset'leri bellekten atar ve yalnızca yerel
        ///       değişkenlerde tutulan referansları görmez. Asset'ler bundan
        ///       önce hazırlanırsa ScriptableObject ve prefab bileşeni
        ///       sarmalayıcıları "null" olur. Malzemeler yeniden yüklenebildiği
        ///       için zemin ve duvarlar yine çizilir, ama istasyon bağlanırken
        ///       "Station.data için atanacak nesne yok" hatası çıkar.</item>
        /// <item>Asset'ler Assets/GameData altına yazılır; ardından SaveAssets + Refresh.</item>
        /// <item>Sahneye bağlanacak her asset diskteki yolundan yeniden yüklenir.
        ///       Yüklenemeyen asset, yolu ve beklenen tipi söyleyerek üretimi durdurur.</item>
        /// </list>
        /// </summary>
        public static SceneSetupResult Generate()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AssetTracker tracker = new AssetTracker();
            EnsureFolders();
            EnsureMaterials(tracker);
            EnsureStationData(tracker);
            EnsureAsset<SoundLibrary>(SoundLibraryPath, tracker, ScriptableObject.CreateInstance<SoundLibrary>);
            EnsureQuests(tracker);
            EnsureCustomerPrefab(tracker);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GameAssets assets = LoadGameAssets();

            Camera camera = CreateCamera();
            CreateLighting();
            Vector3 origin = FindRestaurantOrigin(camera);
            RestaurantRefs restaurant = CreateRestaurant(origin, assets.Stations, assets.Palette);
            CustomerSpawner spawner = CreateCustomerSpawner(restaurant, assets.CustomerPrefab);
            ManagerRefs managers = CreateManagers(assets.SoundLibrary, assets.Quests);
            UIManager uiManager = CreateUserInterface(restaurant);
            WireGameManager(managers, uiManager, restaurant, spawner);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Sahne kaydedilemedi: {ScenePath}");
            }

            AddSceneToBuildSettings(ScenePath);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();

            Debug.Log($"[SceneSetupTool] {ScenePath} üretildi. Yeni asset: {tracker.Created}, korunan: {tracker.Reused}.");
            return new SceneSetupResult(ScenePath, tracker.Created, tracker.Reused, managers.GameManager, uiManager);
        }

        // ── Klasörler ──────────────────────────────────────────────────────────

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "GameData");
            EnsureFolder(DataRoot, "Stations");
            EnsureFolder(DataRoot, "Quests");
            EnsureFolder(DataRoot, "Audio");
            EnsureFolder(DataRoot, "Materials");
            EnsureFolder(MaterialsRoot, ThemeName);
            EnsureFolder(DataRoot, "Prefabs");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string MaterialPath(string name) => $"{MaterialsFolder}/{name}.mat";

        private static string StationDataPath(string stationId) => $"{StationsFolder}/{stationId}.asset";

        private static string QuestPath(string questId) => $"{QuestsFolder}/{questId}.asset";

        // ── Asset'leri diske yazma ─────────────────────────────────────────────

        /// <summary>
        /// Yolda <typeparamref name="T"/> olarak yüklenebilen bir asset varsa
        /// ona dokunmaz (tasarımcının değişiklikleri korunur); yoksa
        /// <paramref name="create"/> ile üretip o yola yazar.
        /// </summary>
        private static void EnsureAsset<T>(string path, AssetTracker tracker, Func<T> create) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                tracker.Reused++;
                return;
            }

            DiscardAsset(path, $"bir {typeof(T).Name} olarak yüklenemiyor");
            AssetDatabase.CreateAsset(create(), path);
            tracker.Created++;
        }

        /// <summary>
        /// Yolda kullanılamayan bir dosya varsa (script'i silinmiş ya da adı
        /// değişmiş bir ScriptableObject, başka tipte bir asset, aracın eski
        /// sürümünden kalan prefab) çöp kutusuna taşır; yerine yenisi
        /// yazılabilsin. Kalıcı silme yalnızca çöp kutusu kullanılamazsa yapılır.
        /// </summary>
        private static void DiscardAsset(string path, string reason)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !File.Exists(path))
            {
                return;
            }

            Debug.LogWarning($"[SceneSetupTool] {path} {reason}; çöp kutusuna taşınıp yeniden üretiliyor.");
            if (!AssetDatabase.MoveAssetToTrash(path) && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException($"{path} kaldırılamadı. Dosyayı elle silip aracı yeniden çalıştırın.");
            }
        }

        private static void EnsureMaterials(AssetTracker tracker)
        {
            Shader shader = FindLitShader();

            foreach (MaterialSpec spec in SharedMaterials)
            {
                EnsureMaterial(spec, shader, tracker);
            }

            foreach (StationSpec station in Stations)
            {
                EnsureMaterial(new MaterialSpec(MaterialName.StationBody(station.Id), station.BodyColor), shader, tracker);
                EnsureMaterial(new MaterialSpec(MaterialName.StationAccent(station.Id), station.AccentColor), shader, tracker);
                EnsureMaterial(new MaterialSpec(MaterialName.Drink(station.Id), station.DrinkColor, 0.5f), shader, tracker);
            }
        }

        private static void EnsureMaterial(MaterialSpec spec, Shader shader, AssetTracker tracker)
        {
            EnsureAsset(MaterialPath(spec.Name), tracker, () => CreateMaterial(spec.Color, spec.Smoothness, shader));
        }

        /// <summary>
        /// Built-in Standard (_Color, _Glossiness) ve URP/HDRP Lit (_BaseColor,
        /// _Smoothness) için aynı rengi ve parlaklığı yazar. Varsayılan mat
        /// yüzey low-poly pastel görünüme yakışıyor; seramik ve tezgah biraz parlak.
        /// </summary>
        private static Material CreateMaterial(Color color, float smoothness, Shader shader)
        {
            Material material = new Material(shader) { color = color };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            return material;
        }

        /// <summary>Etkin render pipeline'a uygun, ışık alan bir shader: URP → HDRP → Built-in Standard.</summary>
        private static Shader FindLitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null)
                {
                    return urp;
                }

                Shader hdrp = Shader.Find("HDRP/Lit");
                if (hdrp != null)
                {
                    return hdrp;
                }
            }

            Shader standard = Shader.Find("Standard");
            if (standard == null)
            {
                throw new InvalidOperationException("Işık alan bir shader bulunamadı (URP/Lit, HDRP/Lit veya Standard).");
            }

            return standard;
        }

        private static void EnsureStationData(AssetTracker tracker)
        {
            foreach (StationSpec spec in Stations)
            {
                EnsureAsset(StationDataPath(spec.Id), tracker, () =>
                {
                    StationData data = ScriptableObject.CreateInstance<StationData>();
                    using (Wiring wiring = new Wiring(data))
                    {
                        wiring.String("stationId", spec.Id)
                            .String("stationName", spec.Name)
                            .Double("baseCost", spec.BaseCost)
                            .Double("baseIncome", spec.BaseIncome)
                            .Float("cycleTime", spec.CycleTime)
                            .Double("costMultiplier", StationData.DefaultCostMultiplier);
                    }

                    return data;
                });
            }
        }

        /// <summary>
        /// Görevler QuestDefinition tipiyle aranır: tasarımcı bir görevin
        /// türünü değiştirdiyse (ör. para kazan → yükselt) o asset korunur.
        /// </summary>
        private static void EnsureQuests(AssetTracker tracker)
        {
            foreach (QuestSpec spec in QuestChain)
            {
                EnsureAsset(QuestPath(spec.Id), tracker, () =>
                {
                    QuestDefinition quest = spec.Create();
                    using (Wiring wiring = new Wiring(quest))
                    {
                        wiring.String("questId", spec.Id)
                            .Double("targetAmount", spec.Target)
                            .Double("targetScalePerCycle", spec.TargetScale)
                            .Double("rewardAmount", spec.Reward)
                            .Double("rewardScalePerCycle", spec.RewardScale)
                            .Float("rewardIncomeSeconds", spec.RewardIncomeSeconds);
                    }

                    return quest;
                });
            }
        }

        /// <summary>
        /// Kök (CustomerController + CustomerVisual) y = 0'da durur ve yürüyen
        /// odur. Görünen parçalar "Model" altında: CustomerVisual yalnızca
        /// Model'i zıplatıp yalpalatır, kökün konumuna dokunmaz. Parçalarda
        /// collider yok; rigidbody'siz hareket eden collider fizik motorunu her
        /// karede yorar ve müşterinin çarpışmaya ihtiyacı yok.
        ///
        /// Var olan prefab CustomerVisual içermiyorsa aracın önceki sürümünden
        /// kalmıştır; çöp kutusuna taşınıp yenisi üretilir.
        /// </summary>
        private static void EnsureCustomerPrefab(AssetTracker tracker)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            bool hasController = existing != null && existing.TryGetComponent(out CustomerController _);
            if (hasController && existing.TryGetComponent(out CustomerVisual _))
            {
                tracker.Reused++;
                return;
            }

            DiscardAsset(CustomerPrefabPath, hasController
                ? "aracın önceki sürümünden kalmış (CustomerVisual yok)"
                : "bir müşteri prefab'ı olarak kullanılamıyor (CustomerController yok)");

            GameObject root = new GameObject("Customer");
            CustomerController controller = root.AddComponent<CustomerController>();
            using (Wiring wiring = new Wiring(controller))
            {
                wiring.Float("moveSpeed", 2.2f).Float("arrivalDistance", 0.05f);
            }

            Transform model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            Material hat = LoadMaterial(MaterialName.CustomerHat);
            Material eye = LoadMaterial(MaterialName.Eye);
            Material blush = LoadMaterial(MaterialName.Blush);

            // Kapsül: yarıçap 0.225, boy 0.9. Yüz +Z'de; CustomerVisual modeli gidiş yönüne çevirir.
            Detail("Body", PrimitiveType.Capsule, model, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.45f, 0.45f),
                LoadMaterial(MaterialName.Customer));
            Detail("Hat", PrimitiveType.Cylinder, model, new Vector3(0f, 0.93f, 0f), new Vector3(0.34f, 0.05f, 0.34f), hat);
            Detail("Pompom", PrimitiveType.Sphere, model, new Vector3(0f, 1f, 0f), new Vector3(0.12f, 0.12f, 0.12f), hat);
            Detail("Eye_L", PrimitiveType.Sphere, model, new Vector3(-0.075f, 0.6f, 0.2f), new Vector3(0.065f, 0.065f, 0.065f), eye);
            Detail("Eye_R", PrimitiveType.Sphere, model, new Vector3(0.075f, 0.6f, 0.2f), new Vector3(0.065f, 0.065f, 0.065f), eye);
            Detail("Cheek_L", PrimitiveType.Sphere, model, new Vector3(-0.125f, 0.52f, 0.18f), new Vector3(0.07f, 0.035f, 0.03f), blush);
            Detail("Cheek_R", PrimitiveType.Sphere, model, new Vector3(0.125f, 0.52f, 0.18f), new Vector3(0.07f, 0.035f, 0.03f), blush);

            CustomerVisual visual = root.AddComponent<CustomerVisual>();
            using (Wiring wiring = new Wiring(visual))
            {
                wiring.Ref("model", model);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CustomerPrefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                throw new InvalidOperationException($"Müşteri prefab'ı kaydedilemedi: {CustomerPrefabPath}");
            }

            tracker.Created++;
        }

        // ── Asset'leri diskten yükleme ─────────────────────────────────────────

        /// <summary>SaveAssets + Refresh'ten sonra: sahneye bağlanacak her asset, diskteki yolundan.</summary>
        private static GameAssets LoadGameAssets()
        {
            GameAssets assets = new GameAssets
            {
                Palette = LoadPalette(),
                Stations = new StationData[Stations.Length],
                SoundLibrary = LoadRequired<SoundLibrary>(SoundLibraryPath),
                Quests = new List<QuestDefinition>(QuestChain.Length),
                CustomerPrefab = LoadCustomerPrefab()
            };

            for (int i = 0; i < Stations.Length; i++)
            {
                assets.Stations[i] = LoadRequired<StationData>(StationDataPath(Stations[i].Id));
            }

            foreach (QuestSpec spec in QuestChain)
            {
                assets.Quests.Add(LoadRequired<QuestDefinition>(QuestPath(spec.Id)));
            }

            return assets;
        }

        private static Palette LoadPalette()
        {
            Palette palette = new Palette
            {
                FloorBase = LoadMaterial(MaterialName.FloorBase),
                Planks = new[]
                {
                    LoadMaterial(MaterialName.WoodLight),
                    LoadMaterial(MaterialName.WoodHoney),
                    LoadMaterial(MaterialName.WoodPale)
                },
                WallMint = LoadMaterial(MaterialName.WallMint),
                WallCream = LoadMaterial(MaterialName.WallCream),
                Trim = LoadMaterial(MaterialName.Trim),
                Rug = LoadMaterial(MaterialName.Rug),
                RugBorder = LoadMaterial(MaterialName.RugBorder),
                Wood = LoadMaterial(MaterialName.Wood),
                CounterTop = LoadMaterial(MaterialName.CounterTop),
                EntryMat = LoadMaterial(MaterialName.EntryMat),
                ExitMat = LoadMaterial(MaterialName.ExitMat),
                Plant = LoadMaterial(MaterialName.Plant),
                Pot = LoadMaterial(MaterialName.Pot),
                Spot = LoadMaterial(MaterialName.Spot),
                Ceramic = LoadMaterial(MaterialName.Ceramic),
                Napkin = LoadMaterial(MaterialName.Napkin),
                Cutlery = LoadMaterial(MaterialName.Cutlery),
                StationBodies = new Material[Stations.Length],
                StationAccents = new Material[Stations.Length],
                Drinks = new Material[Stations.Length]
            };

            for (int i = 0; i < Stations.Length; i++)
            {
                palette.StationBodies[i] = LoadMaterial(MaterialName.StationBody(Stations[i].Id));
                palette.StationAccents[i] = LoadMaterial(MaterialName.StationAccent(Stations[i].Id));
                palette.Drinks[i] = LoadMaterial(MaterialName.Drink(Stations[i].Id));
            }

            return palette;
        }

        private static Material LoadMaterial(string name)
        {
            return LoadRequired<Material>(MaterialPath(name));
        }

        private static CustomerController LoadCustomerPrefab()
        {
            if (!LoadRequired<GameObject>(CustomerPrefabPath).TryGetComponent(out CustomerController controller))
            {
                throw new InvalidOperationException($"{CustomerPrefabPath} kökünde CustomerController yok.");
            }

            return controller;
        }

        /// <summary>
        /// Asset'i diskteki yolundan yükler. Yüklenemezse yolu, beklenen tipi
        /// ve yolda bulunanı söyleyerek durur; sahneye asla null bağlanmaz.
        /// </summary>
        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            Object found = AssetDatabase.LoadMainAssetAtPath(path);
            string foundText = found != null ? found.GetType().Name : File.Exists(path) ? "yüklenemeyen bir dosya" : "dosya yok";
            throw new InvalidOperationException(
                $"{path} bir {typeof(T).Name} olarak yüklenemedi (bulunan: {foundText}). " +
                $"{typeof(T).Name} script dosyasının adı sınıf adıyla aynı olmalı ve proje hatasız derlenmeli. " +
                $"Sorun sürerse {DataRoot} klasörünü silip aracı yeniden çalıştırın.");
        }

        // ── Kamera ve ışık ─────────────────────────────────────────────────────

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;

            // Düz, aydınlık ve sıcak ortam ışığı: pastel yüzler gölgede griye dönmesin.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.7f, 0.67f);
        }

        /// <summary>
        /// Kameranın, ekranın <see cref="ViewportFocusY"/> yüksekliğinden
        /// baktığı zemin noktası. Ortografik kamerada dikey kayma en-boy
        /// oranından bağımsızdır; restoran her telefonda aynı yüksekliğe düşer.
        /// </summary>
        private static Vector3 FindRestaurantOrigin(Camera camera)
        {
            Transform cameraTransform = camera.transform;
            Vector3 rayOrigin = cameraTransform.position + cameraTransform.up * (camera.orthographicSize * (2f * ViewportFocusY - 1f));
            Vector3 direction = cameraTransform.forward;

            if (direction.y >= -0.01f)
            {
                return Vector3.zero;
            }

            Vector3 hit = rayOrigin + direction * (-rayOrigin.y / direction.y);
            return new Vector3(Mathf.Round(hit.x * 10f) / 10f, 0f, Mathf.Round(hit.z * 10f) / 10f);
        }

        // ── Restoran ───────────────────────────────────────────────────────────

        /// <summary>
        /// Restoran, kameranın baktığı yönle hizalı: +x ve +z kenarları arka
        /// duvarlar (kamerayı kapatmaz), -x ve -z kenarları ön taraf. Kapılar
        /// önde: giriş sağ ön kenarda, çıkış sol ön kenarda; istasyonlar arka
        /// duvar boyunca.
        ///
        /// Zemin, duvarlar, bitkiler ve kapı çerçeveleri "Environment" altında
        /// toplanıp Batching Static işaretlenir: onlarca parke tahtası mobilde
        /// birkaç çizim çağrısına iner. İstasyonlar statik değil; ileride
        /// yükseltme animasyonu alabilirler.
        /// </summary>
        private static RestaurantRefs CreateRestaurant(Vector3 origin, StationData[] stationData, Palette palette)
        {
            float half = FloorSize * 0.5f;
            Transform root = new GameObject("Restaurant").transform;
            root.position = origin;

            Transform environment = new GameObject("Environment").transform;
            environment.SetParent(root, false);

            // Taban koyu ahşap: parke tahtaları arasındaki derzler ondan görünür.
            Block("Floor", PrimitiveType.Cube, environment, new Vector3(0f, -0.12f, 0f), new Vector3(FloorSize, 0.2f, FloorSize), palette.FloorBase);
            Parquet(environment, palette);

            Detail("RugBorder", PrimitiveType.Cube, environment, new Vector3(0f, 0.01f, -0.9f), new Vector3(3.5f, 0.02f, 1.9f), palette.RugBorder);
            Detail("Rug", PrimitiveType.Cube, environment, new Vector3(0f, 0.013f, -0.9f), new Vector3(3.2f, 0.02f, 1.6f), palette.Rug);

            Wall("Wall_Right", environment, new Vector3(half + 0.1f, 0f, 0f), true, palette);
            Wall("Wall_Left", environment, new Vector3(0f, 0f, half + 0.1f), false, palette);
            Plant(environment, new Vector3(-half + 0.45f, 0f, half - 0.45f), palette);
            Plant(environment, new Vector3(half - 0.45f, 0f, half - 0.45f), palette);

            RestaurantRefs refs = new RestaurantRefs
            {
                EntryPoint = Door("EntryDoor", environment, root, new Vector3(1.5f, 0f, -half), 0f, new Vector3(1.5f, 0f, -half - 0.8f), palette.EntryMat, palette),
                ExitPoint = Door("ExitDoor", environment, root, new Vector3(-half, 0f, 1f), 90f, new Vector3(-half - 0.8f, 0f, 1f), palette.ExitMat, palette),
                Stations = new List<Station>(),
                Seats = new List<CustomerSeat>()
            };

            MarkBatchingStatic(environment);

            Transform stationsRoot = new GameObject("Stations").transform;
            stationsRoot.SetParent(root, false);

            for (int i = 0; i < Stations.Length; i++)
            {
                float x = (i - (Stations.Length - 1) * 0.5f) * 1.8f;
                CreateStation(stationsRoot, new Vector3(x, 0f, 1.9f), Stations[i], stationData[i], i, palette, refs);
            }

            return refs;
        }

        /// <summary>
        /// Açık ahşap parke: x boyunca uzanan, satır satır kaydırılmış
        /// tahtalar; üç ton sırayla karışır. Tahtaların üstü y = 0'da, aradaki
        /// derzlerden koyu taban görünür.
        /// </summary>
        private static void Parquet(Transform parent, Palette palette)
        {
            float half = FloorSize * 0.5f;
            int rows = Mathf.RoundToInt(FloorSize / PlankWidth);
            Transform parquet = new GameObject("Parquet").transform;
            parquet.SetParent(parent, false);

            for (int row = 0; row < rows; row++)
            {
                float z = -half + (row + 0.5f) * PlankWidth;
                float start = -half;
                float cut = -half + PlankStagger[row % PlankStagger.Length];
                if (cut <= start + 0.01f)
                {
                    cut += PlankLength;
                }

                for (int piece = 0; start < half - 0.001f; piece++)
                {
                    float end = Mathf.Min(cut, half);
                    Material wood = palette.Planks[(row * 5 + piece * 3) % palette.Planks.Length];
                    Detail("Plank", PrimitiveType.Cube, parquet, new Vector3((start + end) * 0.5f, -0.01f, z),
                        new Vector3(end - start - PlankGap, 0.02f, PlankWidth - PlankGap), wood);
                    start = end;
                    cut += PlankLength;
                }
            }
        }

        /// <summary>
        /// İki tonlu duvar: altta nane yeşili lambri, üstte krem; aralarında ve
        /// tepede beyaz pervaz. <paramref name="alongZ"/> true ise duvar z
        /// boyunca uzanır (sağ duvar), değilse x boyunca (sol duvar).
        /// </summary>
        private static void Wall(string name, Transform parent, Vector3 localPosition, bool alongZ, Palette palette)
        {
            Transform wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            wall.localPosition = localPosition;

            float length = FloorSize + 0.4f;
            WallLayer("Wainscot", wall, 0.65f, 0.325f, 0.22f, length, alongZ, palette.WallMint);
            WallLayer("ChairRail", wall, 0.05f, 0.675f, 0.26f, length, alongZ, palette.Trim);
            WallLayer("Upper", wall, 0.7f, 1.05f, 0.2f, length, alongZ, palette.WallCream);
            WallLayer("Crown", wall, 0.05f, 1.425f, 0.26f, length, alongZ, palette.Trim);
        }

        private static void WallLayer(string name, Transform wall, float height, float centerY, float thickness, float length,
            bool alongZ, Material material)
        {
            Vector3 size = alongZ ? new Vector3(thickness, height, length) : new Vector3(length, height, thickness);
            Block(name, PrimitiveType.Cube, wall, new Vector3(0f, centerY, 0f), size, material);
        }

        private static void CreateStation(Transform parent, Vector3 localPosition, StationSpec spec, StationData data,
            int index, Palette palette, RestaurantRefs refs)
        {
            GameObject stationObject = new GameObject("Station_" + spec.Id);
            stationObject.transform.SetParent(parent, false);
            stationObject.transform.localPosition = localPosition;

            Material accent = palette.StationAccents[index];
            Block("Counter", PrimitiveType.Cube, stationObject.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 0.8f), palette.StationBodies[index]);
            Block("CounterTop", PrimitiveType.Cube, stationObject.transform, new Vector3(0f, CounterTopHeight - 0.04f, 0f), new Vector3(1.42f, 0.08f, 0.92f), palette.CounterTop);

            // Her istasyonun silindir tabanlı küçük bir simgesi: burger, fincan, fırın kubbesi.
            // Tezgahın arka yarısında; ön kenar servis takımına kalır.
            switch (index)
            {
                case 0:
                    Detail("Burger", PrimitiveType.Cylinder, stationObject.transform, new Vector3(0f, 1.05f, 0.1f), new Vector3(0.5f, 0.07f, 0.5f), accent);
                    break;
                case 1:
                    Detail("Cup", PrimitiveType.Cylinder, stationObject.transform, new Vector3(0f, 1.1f, 0.1f), new Vector3(0.22f, 0.12f, 0.22f), accent);
                    break;
                default:
                    Detail("OvenDome", PrimitiveType.Cylinder, stationObject.transform, new Vector3(0f, 1.18f, 0.12f), new Vector3(0.8f, 0.2f, 0.6f), accent);
                    break;
            }

            Tableware(stationObject.transform, palette.Drinks[index], palette);

            Transform spot = new GameObject("CustomerSpot").transform;
            spot.SetParent(stationObject.transform, false);
            spot.localPosition = new Vector3(0f, 0f, -1.25f);
            Detail("SpotMarker", PrimitiveType.Cylinder, spot, new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.01f, 0.6f), palette.Spot);

            Station station = stationObject.AddComponent<Station>();
            using (Wiring wiring = new Wiring(station))
            {
                wiring.Ref(Station.DataFieldName, data).Int(Station.StartingLevelFieldName, spec.StartingLevel);
            }

            // Yazılan alan gerçekten Station.Data'nın okuduğu alan mı?
            if (station.Data != data)
            {
                throw new InvalidOperationException(
                    $"Station.{Station.DataFieldName} yazıldı ama Station.Data {data.name} döndürmüyor; alan adı ile özellik uyuşmuyor.");
            }

            CustomerSeat seat = stationObject.AddComponent<CustomerSeat>();
            using (Wiring wiring = new Wiring(seat))
            {
                wiring.Ref("station", station).Ref("standPoint", spot);
            }

            refs.Stations.Add(station);
            refs.Seats.Add(seat);
        }

        /// <summary>
        /// Tezgahın müşteri tarafındaki (-z) kenarda küçük bir servis takımı:
        /// peçete + çatal, tabak ve içecekli bardak. Hepsi placeholder; kendi
        /// modellerinizle değiştirilebilir.
        /// </summary>
        private static void Tableware(Transform station, Material drink, Palette palette)
        {
            Transform set = new GameObject("Tableware").transform;
            set.SetParent(station, false);
            set.localPosition = new Vector3(0f, CounterTopHeight, -0.33f);

            Detail("Napkin", PrimitiveType.Cube, set, new Vector3(-0.56f, 0.002f, 0f), new Vector3(0.13f, 0.004f, 0.2f), palette.Napkin);
            Detail("Fork", PrimitiveType.Cube, set, new Vector3(-0.56f, 0.007f, 0f), new Vector3(0.025f, 0.006f, 0.16f), palette.Cutlery);
            Detail("Plate", PrimitiveType.Cylinder, set, new Vector3(-0.3f, 0.01f, 0f), new Vector3(0.24f, 0.01f, 0.24f), palette.Ceramic);
            Detail("Cup", PrimitiveType.Cylinder, set, new Vector3(0.36f, 0.055f, 0f), new Vector3(0.09f, 0.055f, 0.09f), palette.Ceramic);
            Detail("Drink", PrimitiveType.Cylinder, set, new Vector3(0.36f, 0.112f, 0f), new Vector3(0.075f, 0.002f, 0.075f), drink);
            Detail("Handle", PrimitiveType.Cube, set, new Vector3(0.415f, 0.055f, 0f), new Vector3(0.02f, 0.05f, 0.02f), palette.Ceramic);
        }

        /// <param name="frameParent">Çerçeve ve paspasın ebeveyni (statik ortam).</param>
        /// <param name="pointParent">Müşterilerin yürüdüğü giriş/çıkış noktasının ebeveyni.</param>
        private static Transform Door(string name, Transform frameParent, Transform pointParent, Vector3 localPosition, float yaw,
            Vector3 pointPosition, Material mat, Palette palette)
        {
            Transform door = new GameObject(name).transform;
            door.SetParent(frameParent, false);
            door.localPosition = localPosition;
            door.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Block("Post_L", PrimitiveType.Cube, door, new Vector3(-0.6f, 0.8f, 0f), new Vector3(0.14f, 1.6f, 0.14f), palette.Wood);
            Block("Post_R", PrimitiveType.Cube, door, new Vector3(0.6f, 0.8f, 0f), new Vector3(0.14f, 1.6f, 0.14f), palette.Wood);
            Block("Lintel", PrimitiveType.Cube, door, new Vector3(0f, 1.62f, 0f), new Vector3(1.34f, 0.14f, 0.14f), palette.Wood);
            Detail("DoorMat", PrimitiveType.Cube, door, new Vector3(0f, 0.015f, 0f), new Vector3(1f, 0.02f, 0.6f), mat);

            Transform point = new GameObject(name == "EntryDoor" ? "EntryPoint" : "ExitPoint").transform;
            point.SetParent(pointParent, false);
            point.localPosition = pointPosition;
            return point;
        }

        private static void Plant(Transform parent, Vector3 localPosition, Palette palette)
        {
            Transform plant = new GameObject("Plant").transform;
            plant.SetParent(parent, false);
            plant.localPosition = localPosition;
            Block("Pot", PrimitiveType.Cylinder, plant, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.2f, 0.4f), palette.Pot);
            Block("Leaves", PrimitiveType.Sphere, plant, new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), palette.Plant);
            Detail("LeavesTop", PrimitiveType.Sphere, plant, new Vector3(0.05f, 1.08f, -0.04f), new Vector3(0.42f, 0.42f, 0.42f), palette.Plant);
        }

        private static GameObject Block(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(type);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        /// <summary>
        /// Collider'sız <see cref="Block"/>: yalnızca görünen küçük parçalar
        /// (parke, tabak, göz...). Hiçbiri tıklanmıyor veya çarpışmıyor; her
        /// biri için collider fizik sahnesine boşuna yük olurdu.
        /// </summary>
        private static GameObject Detail(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject detail = Block(name, type, parent, localPosition, localScale, material);
            if (detail.TryGetComponent(out Collider collider))
            {
                Object.DestroyImmediate(collider);
            }

            return detail;
        }

        private static void MarkBatchingStatic(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.BatchingStatic);
            }
        }

        // ── Müşteri akışı ──────────────────────────────────────────────────────

        private static CustomerSpawner CreateCustomerSpawner(RestaurantRefs restaurant, CustomerController prefab)
        {
            GameObject spawnerObject = new GameObject("CustomerSpawner");
            Transform customers = new GameObject("Customers").transform;
            customers.SetParent(spawnerObject.transform, false);

            CustomerSpawner spawner = spawnerObject.AddComponent<CustomerSpawner>();
            using (Wiring wiring = new Wiring(spawner))
            {
                wiring.Ref("customerPrefab", prefab)
                    .Ref("customerParent", customers)
                    .Ref("entryPoint", restaurant.EntryPoint)
                    .Ref("exitPoint", restaurant.ExitPoint)
                    .RefList("seats", restaurant.Seats);
            }

            return spawner;
        }

        // ── Yöneticiler ────────────────────────────────────────────────────────

        private static ManagerRefs CreateManagers(SoundLibrary soundLibrary, List<QuestDefinition> quests)
        {
            GameObject managersObject = new GameObject("---MANAGERS---");

            ManagerRefs managers = new ManagerRefs
            {
                GameManager = managersObject.AddComponent<GameManager>(),
                Currency = managersObject.AddComponent<CurrencyManager>(),
                Save = managersObject.AddComponent<SaveManager>(),
                Ads = managersObject.AddComponent<AdManager>(),
                Audio = managersObject.AddComponent<AudioManager>(),
                AudioBinder = managersObject.AddComponent<AudioEventBinder>(),
                Prestige = managersObject.AddComponent<PrestigeManager>(),
                Quests = managersObject.AddComponent<QuestManager>()
            };

            using (Wiring wiring = new Wiring(managers.Audio))
            {
                wiring.Ref("soundLibrary", soundLibrary);
            }

            using (Wiring wiring = new Wiring(managers.Quests))
            {
                wiring.RefList("questChain", quests);
            }

            return managers;
        }

        private static void WireGameManager(ManagerRefs managers, UIManager uiManager, RestaurantRefs restaurant, CustomerSpawner spawner)
        {
            using (Wiring wiring = new Wiring(managers.GameManager))
            {
                wiring.Ref("currencyManager", managers.Currency)
                    .Ref("saveManager", managers.Save)
                    .Ref("adManager", managers.Ads)
                    .Ref("uiManager", uiManager)
                    .Ref("prestigeManager", managers.Prestige)
                    .Ref("questManager", managers.Quests)
                    .Ref("audioManager", managers.Audio)
                    .Ref("audioEventBinder", managers.AudioBinder)
                    .Ref("customerSpawner", spawner)
                    .RefList("stations", restaurant.Stations);
            }
        }

        // ── Arayüz ─────────────────────────────────────────────────────────────

        private static UIManager CreateUserInterface(RestaurantRefs restaurant)
        {
            GameObject canvasObject = new GameObject("UI Canvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Transform ui = canvasObject.transform;
            UiParts parts = new UiParts();

            BuildTopBar(ui, parts);
            QuestPanelUI questPanel = BuildQuestCard(ui);
            BuildStationRows(ui, restaurant, parts);
            BuildBottomBar(ui, parts);
            parts.ToastText = CreateText("Toast", ui, "", 44f, UiStyle.DarkText, TextAlignmentOptions.Center, true);
            AnchorBottomStretch(parts.ToastText.rectTransform, 820f, 90f, 60f, 60f);
            parts.ToastText.gameObject.SetActive(false);

            BuildOfflinePopup(ui, parts);
            PrestigePanelUI prestigePanel = BuildPrestigePanel(ui, parts.PrestigeOpenButton);
            SettingsPanelUI settingsPanel = BuildSettingsPanel(ui);

            UIManager uiManager = canvasObject.AddComponent<UIManager>();
            using (Wiring wiring = new Wiring(uiManager))
            {
                wiring.Ref("currencyText", parts.CurrencyText)
                    .Ref("incomePerSecondText", parts.IncomeText)
                    .Ref("offlinePopup", parts.OfflinePopup)
                    .Ref("offlineAmountText", parts.OfflineAmountText)
                    .Ref("offlineDurationText", parts.OfflineDurationText)
                    .Ref("offlineCollectButton", parts.OfflineCollectButton)
                    .Ref("offlineDoubleButton", parts.OfflineDoubleButton)
                    .Ref("offlineDoubleButtonText", parts.OfflineDoubleButtonText)
                    .Ref("speedBoostButton", parts.BoostButton)
                    .Ref("speedBoostText", parts.BoostText)
                    .Ref("toastText", parts.ToastText)
                    .Ref("questPanel", questPanel)
                    .Ref("prestigePanel", prestigePanel)
                    .Ref("settingsButton", parts.SettingsButton)
                    .Ref("settingsPanel", settingsPanel);

                SerializedProperty views = wiring.Find("stationViews");
                views.arraySize = parts.StationRows.Count;
                for (int i = 0; i < parts.StationRows.Count; i++)
                {
                    StationRow row = parts.StationRows[i];
                    SerializedProperty view = views.GetArrayElementAtIndex(i);
                    Wiring.Relative(view, "station").objectReferenceValue = row.Station;
                    Wiring.Relative(view, "nameText").objectReferenceValue = row.Name;
                    Wiring.Relative(view, "levelText").objectReferenceValue = row.Level;
                    Wiring.Relative(view, "incomeText").objectReferenceValue = row.Income;
                    Wiring.Relative(view, "upgradeCostText").objectReferenceValue = row.Cost;
                    Wiring.Relative(view, "upgradeButton").objectReferenceValue = row.Button;
                    Wiring.Relative(view, "progressFill").objectReferenceValue = row.Progress;
                }
            }

            CreateEventSystem();
            return uiManager;
        }

        private static void BuildTopBar(Transform ui, UiParts parts)
        {
            Image bar = Panel("TopBar", ui, UiStyle.TopBar, false);
            AnchorTopStretch(bar.rectTransform, 0f, 230f, 0f, 0f);

            parts.CurrencyText = CreateText("CurrencyText", bar.transform, "0", 88f, UiStyle.Gold, TextAlignmentOptions.Left, true);
            AnchorTopLeft(parts.CurrencyText.rectTransform, 40f, 24f, 720f, 116f);

            parts.IncomeText = CreateText("IncomePerSecondText", bar.transform, "0 / sn", 40f, UiStyle.SoftWhite, TextAlignmentOptions.Left, false);
            AnchorTopLeft(parts.IncomeText.rectTransform, 44f, 142f, 720f, 60f);

            TMP_Text settingsLabel;
            parts.SettingsButton = CreateButton("SettingsButton", bar.transform, "Ayarlar", UiStyle.Neutral, 38f, out settingsLabel);
            AnchorTopRight(parts.SettingsButton.GetComponent<RectTransform>(), 30f, 50f, 230f, 120f);
        }

        private static QuestPanelUI BuildQuestCard(Transform ui)
        {
            Image card = Panel("QuestCard", ui, UiStyle.Card, true);
            AnchorTopStretch(card.rectTransform, 250f, 200f, 30f, 30f);

            TMP_Text title = CreateText("Title", card.transform, "Görev", 40f, UiStyle.DarkText, TextAlignmentOptions.Left, true);
            AnchorTopLeft(title.rectTransform, 30f, 16f, 640f, 54f);

            TMP_Text reward = CreateText("Reward", card.transform, "Ödül: 0", 32f, UiStyle.Green, TextAlignmentOptions.Right, true);
            AnchorTopRight(reward.rectTransform, 30f, 18f, 340f, 50f);

            TMP_Text description = CreateText("Description", card.transform, "", 34f, UiStyle.MutedText, TextAlignmentOptions.Left, false);
            AnchorTopLeft(description.rectTransform, 30f, 72f, 960f, 50f);

            Image fill;
            ProgressBar("Progress", card.transform, out fill);
            AnchorBottomStretch(fill.transform.parent.GetComponent<RectTransform>(), 22f, 26f, 30f, 260f);

            TMP_Text progress = CreateText("ProgressText", card.transform, "0 / 0", 30f, UiStyle.DarkText, TextAlignmentOptions.Right, false);
            AnchorBottomRight(progress.rectTransform, 30f, 12f, 220f, 46f);

            TMP_Text claimLabel;
            Button claim = CreateButton("ClaimButton", card.transform, "Topla", UiStyle.Green, 34f, out claimLabel);
            AnchorBottomRight(claim.GetComponent<RectTransform>(), 30f, 60f, 200f, 70f);
            claim.gameObject.SetActive(false);

            QuestPanelUI panel = card.gameObject.AddComponent<QuestPanelUI>();
            using (Wiring wiring = new Wiring(panel))
            {
                wiring.Ref("titleText", title)
                    .Ref("descriptionText", description)
                    .Ref("progressText", progress)
                    .Ref("rewardText", reward)
                    .Ref("progressFill", fill)
                    .Ref("claimButton", claim);
            }

            return panel;
        }

        private static void BuildStationRows(Transform ui, RestaurantRefs restaurant, UiParts parts)
        {
            const float rowHeight = 170f;
            const float rowSpacing = 16f;
            const float firstRowBottom = 190f;

            for (int i = 0; i < restaurant.Stations.Count; i++)
            {
                // İlk istasyon en üstte.
                float bottom = firstRowBottom + (restaurant.Stations.Count - 1 - i) * (rowHeight + rowSpacing);
                Image row = Panel("StationRow_" + Stations[i].Id, ui, UiStyle.Card, true);
                AnchorBottomStretch(row.rectTransform, bottom, rowHeight, 30f, 30f);

                TMP_Text name = CreateText("Name", row.transform, Stations[i].Name, 40f, UiStyle.DarkText, TextAlignmentOptions.Left, true);
                AnchorTopLeft(name.rectTransform, 30f, 16f, 560f, 56f);

                TMP_Text level = CreateText("Level", row.transform, "Sv. 0", 32f, UiStyle.MutedText, TextAlignmentOptions.Left, false);
                AnchorBottomLeft(level.rectTransform, 30f, 34f, 220f, 48f);

                TMP_Text income = CreateText("Income", row.transform, "", 32f, UiStyle.Green, TextAlignmentOptions.Left, false);
                AnchorBottomLeft(income.rectTransform, 260f, 34f, 360f, 48f);

                Image fill;
                ProgressBar("Progress", row.transform, out fill);
                AnchorBottomStretch(fill.transform.parent.GetComponent<RectTransform>(), 14f, 14f, 30f, 380f);

                TMP_Text cost;
                Button button = CreateButton("UpgradeButton", row.transform, "Yükselt", UiStyle.Orange, 36f, out cost);
                AnchorRightCenter(button.GetComponent<RectTransform>(), 22f, 330f, 130f);

                parts.StationRows.Add(new StationRow
                {
                    Station = restaurant.Stations[i],
                    Name = name,
                    Level = level,
                    Income = income,
                    Cost = cost,
                    Button = button,
                    Progress = fill
                });
            }
        }

        private static void BuildBottomBar(Transform ui, UiParts parts)
        {
            parts.BoostButton = CreateButton("SpeedBoostButton", ui, "2x Hız", UiStyle.Purple, 36f, out parts.BoostText);
            RectTransform boost = parts.BoostButton.GetComponent<RectTransform>();
            boost.anchorMin = new Vector2(0f, 0f);
            boost.anchorMax = new Vector2(0.5f, 0f);
            boost.pivot = new Vector2(0.5f, 0f);
            boost.offsetMin = new Vector2(30f, 36f);
            boost.offsetMax = new Vector2(-10f, 36f + 130f);

            TMP_Text prestigeLabel;
            parts.PrestigeOpenButton = CreateButton("PrestigeButton", ui, "Prestij", UiStyle.Gem, 36f, out prestigeLabel);
            RectTransform prestige = parts.PrestigeOpenButton.GetComponent<RectTransform>();
            prestige.anchorMin = new Vector2(0.5f, 0f);
            prestige.anchorMax = new Vector2(1f, 0f);
            prestige.pivot = new Vector2(0.5f, 0f);
            prestige.offsetMin = new Vector2(10f, 36f);
            prestige.offsetMax = new Vector2(-30f, 36f + 130f);
        }

        private static void BuildOfflinePopup(Transform ui, UiParts parts)
        {
            Image box;
            GameObject popup = Modal("OfflinePopup", ui, 860f, 760f, out box);

            TMP_Text title = CreateText("Title", box.transform, "Tekrar hoş geldin!", 52f, UiStyle.DarkText, TextAlignmentOptions.Center, true);
            AnchorTopStretch(title.rectTransform, 50f, 80f, 40f, 40f);

            parts.OfflineDurationText = CreateText("Duration", box.transform, "", 36f, UiStyle.MutedText, TextAlignmentOptions.Center, false);
            AnchorTopStretch(parts.OfflineDurationText.rectTransform, 150f, 60f, 40f, 40f);

            parts.OfflineAmountText = CreateText("Amount", box.transform, "+0", 96f, UiStyle.Gold, TextAlignmentOptions.Center, true);
            AnchorTopStretch(parts.OfflineAmountText.rectTransform, 240f, 140f, 40f, 40f);

            parts.OfflineDoubleButton = CreateButton("DoubleButton", box.transform, "2x (Reklam)", UiStyle.Purple, 40f, out parts.OfflineDoubleButtonText);
            AnchorBottomStretch(parts.OfflineDoubleButton.GetComponent<RectTransform>(), 190f, 130f, 80f, 80f);

            TMP_Text collectLabel;
            parts.OfflineCollectButton = CreateButton("CollectButton", box.transform, "Topla", UiStyle.Green, 40f, out collectLabel);
            AnchorBottomStretch(parts.OfflineCollectButton.GetComponent<RectTransform>(), 40f, 130f, 80f, 80f);

            parts.OfflinePopup = popup;
            popup.SetActive(false);
        }

        private static PrestigePanelUI BuildPrestigePanel(Transform ui, Button openButton)
        {
            Image box;
            GameObject root = Modal("PrestigePanel", ui, 920f, 1260f, out box);

            TMP_Text title = CreateText("Title", box.transform, "Prestij", 56f, UiStyle.DarkText, TextAlignmentOptions.Center, true);
            AnchorTopStretch(title.rectTransform, 40f, 80f, 40f, 40f);

            TMP_Text gems = CreateText("Gems", box.transform, "0 Gem", 52f, UiStyle.Gem, TextAlignmentOptions.Center, true);
            AnchorTopStretch(gems.rectTransform, 130f, 70f, 40f, 40f);

            TMP_Text multipliers = CreateText("Multipliers", box.transform, "", 34f, UiStyle.MutedText, TextAlignmentOptions.Center, false);
            AnchorTopStretch(multipliers.rectTransform, 205f, 50f, 40f, 40f);

            TMP_Text pending = CreateText("PendingGems", box.transform, "", 38f, UiStyle.DarkText, TextAlignmentOptions.Center, true);
            AnchorTopStretch(pending.rectTransform, 290f, 56f, 40f, 40f);

            TMP_Text nextGem = CreateText("NextGem", box.transform, "", 30f, UiStyle.MutedText, TextAlignmentOptions.Center, false);
            AnchorTopStretch(nextGem.rectTransform, 350f, 46f, 40f, 40f);

            TMP_Text prestigeLabel;
            Button prestige = CreateButton("ResetRestaurantButton", box.transform, "Restoranı Sıfırla", UiStyle.Gem, 40f, out prestigeLabel);
            AnchorTopStretch(prestige.GetComponent<RectTransform>(), 420f, 130f, 110f, 110f);

            PrestigeRowParts speed = PrestigeUpgradeRow(box.transform, "SpeedUpgrade", 600f);
            PrestigeRowParts income = PrestigeUpgradeRow(box.transform, "IncomeUpgrade", 800f);

            TMP_Text closeLabel;
            Button close = CreateButton("CloseButton", box.transform, "Kapat", UiStyle.Neutral, 38f, out closeLabel);
            AnchorBottomStretch(close.GetComponent<RectTransform>(), 40f, 110f, 250f, 250f);

            // Panel açma/kapama için ek betik gerekmesin: kalıcı onClick dinleyicileri.
            UnityEventTools.AddBoolPersistentListener(openButton.onClick, root.SetActive, true);
            UnityEventTools.AddBoolPersistentListener(close.onClick, root.SetActive, false);

            PrestigePanelUI panel = root.AddComponent<PrestigePanelUI>();
            using (Wiring wiring = new Wiring(panel))
            {
                wiring.Ref("gemsText", gems)
                    .Ref("pendingGemsText", pending)
                    .Ref("nextGemText", nextGem)
                    .Ref("multipliersText", multipliers)
                    .Ref("prestigeButton", prestige)
                    .Ref("prestigeButtonText", prestigeLabel);

                SerializedProperty rows = wiring.Find("upgradeRows");
                rows.arraySize = 2;
                BindPrestigeRow(rows.GetArrayElementAtIndex(0), "global_speed", speed);
                BindPrestigeRow(rows.GetArrayElementAtIndex(1), "global_income", income);
            }

            root.SetActive(false);
            return panel;
        }

        private static PrestigeRowParts PrestigeUpgradeRow(Transform parent, string name, float top)
        {
            Image row = Panel(name, parent, UiStyle.RowTint, true);
            AnchorTopStretch(row.rectTransform, top, 170f, 50f, 50f);

            PrestigeRowParts parts = new PrestigeRowParts
            {
                Name = CreateText("Name", row.transform, "", 38f, UiStyle.DarkText, TextAlignmentOptions.Left, true),
                Level = CreateText("Level", row.transform, "Sv. 0", 30f, UiStyle.MutedText, TextAlignmentOptions.Left, false),
                Bonus = CreateText("Bonus", row.transform, "+%0", 30f, UiStyle.Green, TextAlignmentOptions.Left, false)
            };

            AnchorTopLeft(parts.Name.rectTransform, 26f, 16f, 480f, 54f);
            AnchorBottomLeft(parts.Level.rectTransform, 26f, 22f, 200f, 46f);
            AnchorBottomLeft(parts.Bonus.rectTransform, 240f, 22f, 260f, 46f);

            parts.Buy = CreateButton("BuyButton", row.transform, "0 Gem", UiStyle.Gem, 34f, out parts.Cost);
            AnchorRightCenter(parts.Buy.GetComponent<RectTransform>(), 20f, 270f, 120f);
            return parts;
        }

        private static void BindPrestigeRow(SerializedProperty row, string upgradeId, PrestigeRowParts parts)
        {
            Wiring.Relative(row, "upgradeId").stringValue = upgradeId;
            Wiring.Relative(row, "nameText").objectReferenceValue = parts.Name;
            Wiring.Relative(row, "levelText").objectReferenceValue = parts.Level;
            Wiring.Relative(row, "bonusText").objectReferenceValue = parts.Bonus;
            Wiring.Relative(row, "costText").objectReferenceValue = parts.Cost;
            Wiring.Relative(row, "buyButton").objectReferenceValue = parts.Buy;
        }

        private static SettingsPanelUI BuildSettingsPanel(Transform ui)
        {
            Image box;
            GameObject root = Modal("SettingsPanel", ui, 900f, 1320f, out box);

            TMP_Text title = CreateText("Title", box.transform, "Ayarlar", 56f, UiStyle.DarkText, TextAlignmentOptions.Center, true);
            AnchorTopStretch(title.rectTransform, 40f, 80f, 40f, 40f);

            TMP_Text sfxLabel;
            Toggle sfx = ToggleRow("SfxToggle", box.transform, "Ses: Açık", 150f, out sfxLabel);
            TMP_Text hapticsLabel;
            Toggle haptics = ToggleRow("HapticsToggle", box.transform, "Titreşim: Açık", 270f, out hapticsLabel);

            TMP_Text policyLabel;
            Button privacyPolicy = CreateButton("PrivacyPolicyButton", box.transform, "Gizlilik Politikası", UiStyle.Neutral, 38f, out policyLabel);
            AnchorTopStretch(privacyPolicy.GetComponent<RectTransform>(), 420f, 120f, 80f, 80f);

            TMP_Text optionsLabel;
            Button privacyOptions = CreateButton("PrivacyOptionsButton", box.transform, "Gizlilik Tercihleri", UiStyle.Neutral, 38f, out optionsLabel);
            AnchorTopStretch(privacyOptions.GetComponent<RectTransform>(), 560f, 120f, 80f, 80f);

            TMP_Text resetLabel;
            Button reset = CreateButton("ResetProgressButton", box.transform, "İlerlemeyi Sıfırla", UiStyle.Red, 38f, out resetLabel);
            AnchorTopStretch(reset.GetComponent<RectTransform>(), 700f, 120f, 80f, 80f);

            TMP_Text version = CreateText("Version", box.transform, "", 28f, UiStyle.MutedText, TextAlignmentOptions.Center, false);
            AnchorBottomStretch(version.rectTransform, 170f, 44f, 40f, 40f);

            TMP_Text closeLabel;
            Button close = CreateButton("CloseButton", box.transform, "Kapat", UiStyle.Neutral, 38f, out closeLabel);
            AnchorBottomStretch(close.GetComponent<RectTransform>(), 40f, 110f, 250f, 250f);

            // İki aşamalı sıfırlama onayı; panelin üstünde.
            Image confirmBox;
            GameObject confirm = Modal("ResetConfirmPopup", root.transform, 780f, 620f, out confirmBox);
            TMP_Text message = CreateText("Message", confirmBox.transform, "", 38f, UiStyle.DarkText, TextAlignmentOptions.Center, false);
            AnchorTopStretch(message.rectTransform, 50f, 300f, 50f, 50f);

            TMP_Text confirmLabel;
            Button confirmButton = CreateButton("ConfirmButton", confirmBox.transform, "Devam", UiStyle.Red, 38f, out confirmLabel);
            AnchorBottomStretch(confirmButton.GetComponent<RectTransform>(), 170f, 110f, 90f, 90f);

            TMP_Text cancelLabel;
            Button cancel = CreateButton("CancelButton", confirmBox.transform, "Vazgeç", UiStyle.Neutral, 38f, out cancelLabel);
            AnchorBottomStretch(cancel.GetComponent<RectTransform>(), 40f, 110f, 90f, 90f);
            confirm.SetActive(false);

            SettingsPanelUI panel = root.AddComponent<SettingsPanelUI>();
            using (Wiring wiring = new Wiring(panel))
            {
                wiring.Ref("closeButton", close)
                    .Ref("resetButton", reset)
                    .Ref("resetConfirmPopup", confirm)
                    .Ref("resetConfirmMessage", message)
                    .Ref("resetConfirmButton", confirmButton)
                    .Ref("resetConfirmButtonText", confirmLabel)
                    .Ref("resetCancelButton", cancel)
                    .Ref("privacyPolicyButton", privacyPolicy)
                    .Ref("privacyOptionsButton", privacyOptions)
                    .Ref("versionText", version);

                SerializedProperty sfxSwitch = wiring.Find("sfxSwitch");
                Wiring.Relative(sfxSwitch, "toggle").objectReferenceValue = sfx;
                Wiring.Relative(sfxSwitch, "label").objectReferenceValue = sfxLabel;

                SerializedProperty hapticsSwitch = wiring.Find("hapticsSwitch");
                Wiring.Relative(hapticsSwitch, "toggle").objectReferenceValue = haptics;
                Wiring.Relative(hapticsSwitch, "label").objectReferenceValue = hapticsLabel;
            }

            root.SetActive(false);
            return panel;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // Yalnızca yeni Input System etkinse eski modül her karede hata verir.
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        // ── Arayüz yapı taşları ────────────────────────────────────────────────

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Panel(string name, Transform parent, Color color, bool rounded)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = UiStyle.RoundedSprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
            TextAlignmentOptions alignment, bool bold)
        {
            TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (bold)
            {
                text.fontStyle = FontStyles.Bold;
            }

            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color, float fontSize, out TMP_Text labelText)
        {
            Image background = Panel(name, parent, color, true);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TextMeshProUGUI text = CreateText("Label", background.transform, label, fontSize, Color.white, TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform, 12f);
            labelText = text;
            return button;
        }

        /// <summary>Arka plan + dolgu. Dolgu Image.Type.Filled; UIManager fillAmount'u sürer.</summary>
        private static void ProgressBar(string name, Transform parent, out Image fill)
        {
            Image background = Panel(name, parent, UiStyle.ProgressBack, true);
            fill = Panel("Fill", background.transform, UiStyle.ProgressFill, true);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, 0f);
        }

        private static Toggle ToggleRow(string name, Transform parent, string label, float top, out TMP_Text labelText)
        {
            RectTransform row = CreateRect(name, parent);
            AnchorTopStretch(row, top, 100f, 80f, 80f);

            Toggle toggle = row.gameObject.AddComponent<Toggle>();

            Image background = Panel("Background", row, UiStyle.ProgressBack, true);
            AnchorLeftCenter(background.rectTransform, 0f, 86f, 86f);

            Image checkmark = Panel("Checkmark", background.transform, UiStyle.Green, false);
            checkmark.sprite = UiStyle.CheckmarkSprite;
            Stretch(checkmark.rectTransform, 10f);

            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;

            labelText = CreateText("Label", row, label, 40f, UiStyle.DarkText, TextAlignmentOptions.Left, false);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(120f, 0f);
            labelRect.offsetMax = Vector2.zero;
            return toggle;
        }

        /// <summary>
        /// Tam ekran karartma (arkadaki tıklamaları da keser) + ortada kutu.
        /// Döndürülen kök açılıp kapatılır.
        /// </summary>
        private static GameObject Modal(string name, Transform parent, float width, float height, out Image box)
        {
            Image dim = Panel(name, parent, UiStyle.Dim, false);
            Stretch(dim.rectTransform, 0f);

            box = Panel("Box", dim.transform, UiStyle.Card, true);
            RectTransform rect = box.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return dim.gameObject;
        }

        // ── RectTransform yerleşim yardımcıları ────────────────────────────────

        private static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        private static void AnchorTopStretch(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void AnchorBottomStretch(RectTransform rect, float bottom, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        private static void AnchorTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            SetCorner(rect, new Vector2(0f, 1f), new Vector2(left, -top), width, height);
        }

        private static void AnchorTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            SetCorner(rect, new Vector2(1f, 1f), new Vector2(-right, -top), width, height);
        }

        private static void AnchorBottomLeft(RectTransform rect, float left, float bottom, float width, float height)
        {
            SetCorner(rect, new Vector2(0f, 0f), new Vector2(left, bottom), width, height);
        }

        private static void AnchorBottomRight(RectTransform rect, float right, float bottom, float width, float height)
        {
            SetCorner(rect, new Vector2(1f, 0f), new Vector2(-right, bottom), width, height);
        }

        private static void AnchorRightCenter(RectTransform rect, float right, float width, float height)
        {
            SetCorner(rect, new Vector2(1f, 0.5f), new Vector2(-right, 0f), width, height);
        }

        private static void AnchorLeftCenter(RectTransform rect, float left, float width, float height)
        {
            SetCorner(rect, new Vector2(0f, 0.5f), new Vector2(left, 0f), width, height);
        }

        /// <summary>Çapa ve pivot aynı köşede: konum, köşeden içeri doğru ölçülür.</summary>
        private static void SetCorner(RectTransform rect, Vector2 corner, Vector2 position, float width, float height)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
        }

        // ── Build Settings ─────────────────────────────────────────────────────

        /// <summary>
        /// Sahneyi listenin başına koyar: "İlerlemeyi Sıfırla" sahneyi build
        /// index'iyle yeniden yükler ve yalnızca listedeki sahneler yüklenebilir.
        /// </summary>
        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ── Serileştirilmiş alan yazımı ────────────────────────────────────────

        /// <summary>
        /// SerializedObject üzerinden alan yazar. Alan bulunamazsa (yeniden
        /// adlandırılmış veya serileştirilmeyen bir alan) hangi bileşenin
        /// hangi alanı olduğunu söyleyerek durur.
        /// </summary>
        private sealed class Wiring : IDisposable
        {
            private readonly SerializedObject _serialized;

            public Wiring(Object target)
            {
                _serialized = new SerializedObject(target);
            }

            public SerializedProperty Find(string field)
            {
                SerializedProperty property = _serialized.FindProperty(field);
                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"{_serialized.targetObject.GetType().Name}.{field} bulunamadı; alan yeniden adlandırılmış olabilir.");
                }

                return property;
            }

            public static SerializedProperty Relative(SerializedProperty parent, string field)
            {
                SerializedProperty property = parent.FindPropertyRelative(field);
                if (property == null)
                {
                    throw new InvalidOperationException($"{parent.propertyPath}.{field} bulunamadı; alan yeniden adlandırılmış olabilir.");
                }

                return property;
            }

            public Wiring Ref(string field, Object value)
            {
                RequireValue(field, value);
                Find(field).objectReferenceValue = value;
                return this;
            }

            public Wiring RefList<T>(string field, IList<T> values) where T : Object
            {
                for (int i = 0; i < values.Count; i++)
                {
                    RequireValue($"{field}[{i}]", values[i]);
                }

                SerializedProperty property = Find(field);
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                return this;
            }

            /// <summary>
            /// Unity'de yok edilmiş veya bellekten atılmış bir nesne de null
            /// sayılır. Böyle bir referans sahneye "None" olarak kaydedilirdi;
            /// onun yerine hangi alanın boş kaldığını söyleyerek durur.
            /// </summary>
            private void RequireValue(string field, Object value)
            {
                if (value == null)
                {
                    throw new InvalidOperationException(
                        $"{_serialized.targetObject.GetType().Name}.{field} için atanacak nesne yok " +
                        "(null, yok edilmiş ya da bellekten atılmış). Bağlanacak asset veya bileşen üretilemedi.");
                }
            }

            public Wiring String(string field, string value)
            {
                Find(field).stringValue = value;
                return this;
            }

            public Wiring Int(string field, int value)
            {
                Find(field).intValue = value;
                return this;
            }

            public Wiring Float(string field, float value)
            {
                Find(field).floatValue = value;
                return this;
            }

            public Wiring Double(string field, double value)
            {
                Find(field).doubleValue = value;
                return this;
            }

            public void Dispose()
            {
                _serialized.ApplyModifiedPropertiesWithoutUndo();
                _serialized.Dispose();
            }
        }

        // ── Veri tipleri ───────────────────────────────────────────────────────

        private sealed class AssetTracker
        {
            public int Created;
            public int Reused;
        }

        private readonly struct StationSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly double BaseCost;
            public readonly double BaseIncome;
            public readonly float CycleTime;
            public readonly int StartingLevel;
            public readonly Color BodyColor;
            public readonly Color AccentColor;

            /// <summary>Tezgahtaki bardağın içeceği (burger: gazoz, kahve: kahve, pizza: limonata).</summary>
            public readonly Color DrinkColor;

            public StationSpec(string id, string name, double baseCost, double baseIncome, float cycleTime,
                int startingLevel, Color bodyColor, Color accentColor, Color drinkColor)
            {
                Id = id;
                Name = name;
                BaseCost = baseCost;
                BaseIncome = baseIncome;
                CycleTime = cycleTime;
                StartingLevel = startingLevel;
                BodyColor = bodyColor;
                AccentColor = accentColor;
                DrinkColor = drinkColor;
            }
        }

        private readonly struct QuestSpec
        {
            public readonly string Id;
            public readonly Func<QuestDefinition> Create;
            public readonly double Target;
            public readonly double TargetScale;
            public readonly double Reward;
            public readonly double RewardScale;
            public readonly float RewardIncomeSeconds;

            public QuestSpec(string id, Func<QuestDefinition> create, double target, double targetScale, double reward,
                double rewardScale, float rewardIncomeSeconds)
            {
                Id = id;
                Create = create;
                Target = target;
                TargetScale = targetScale;
                Reward = reward;
                RewardScale = rewardScale;
                RewardIncomeSeconds = rewardIncomeSeconds;
            }
        }

        private readonly struct MaterialSpec
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly float Smoothness;

            /// <param name="smoothness">0 = tamamen mat. Varsayılan hafif mat; low-poly pastel görünüm için.</param>
            public MaterialSpec(string name, Color color, float smoothness = 0.15f)
            {
                Name = name;
                Color = color;
                Smoothness = smoothness;
            }
        }

        /// <summary>Tema klasöründeki malzeme adları.</summary>
        private static class MaterialName
        {
            public const string FloorBase = "FloorBase";
            public const string WoodLight = "Parquet_Light";
            public const string WoodHoney = "Parquet_Honey";
            public const string WoodPale = "Parquet_Pale";
            public const string WallMint = "Wall_Mint";
            public const string WallCream = "Wall_Cream";
            public const string Trim = "Trim";
            public const string Rug = "Rug";
            public const string RugBorder = "RugBorder";
            public const string Wood = "Wood";
            public const string CounterTop = "CounterTop";
            public const string EntryMat = "EntryMat";
            public const string ExitMat = "ExitMat";
            public const string Plant = "Plant";
            public const string Pot = "Pot";
            public const string Spot = "CustomerSpot";
            public const string Ceramic = "Ceramic";
            public const string Napkin = "Napkin";
            public const string Cutlery = "Cutlery";
            public const string Customer = "Customer";
            public const string CustomerHat = "CustomerHat";
            public const string Eye = "Eye";
            public const string Blush = "Blush";

            public static string StationBody(string stationId) => "Station_" + stationId;

            public static string StationAccent(string stationId) => "Accent_" + stationId;

            public static string Drink(string stationId) => "Drink_" + stationId;
        }

        /// <summary>Diskten yeniden yüklenmiş, sahneye bağlanacak asset'ler.</summary>
        private sealed class GameAssets
        {
            public Palette Palette;
            public StationData[] Stations;
            public SoundLibrary SoundLibrary;
            public List<QuestDefinition> Quests;
            public CustomerController CustomerPrefab;
        }

        private sealed class Palette
        {
            public Material FloorBase;
            public Material[] Planks;
            public Material WallMint;
            public Material WallCream;
            public Material Trim;
            public Material Rug;
            public Material RugBorder;
            public Material Wood;
            public Material CounterTop;
            public Material EntryMat;
            public Material ExitMat;
            public Material Plant;
            public Material Pot;
            public Material Spot;
            public Material Ceramic;
            public Material Napkin;
            public Material Cutlery;
            public Material[] StationBodies;
            public Material[] StationAccents;
            public Material[] Drinks;
        }

        private sealed class RestaurantRefs
        {
            public Transform EntryPoint;
            public Transform ExitPoint;
            public List<Station> Stations;
            public List<CustomerSeat> Seats;
        }

        private sealed class ManagerRefs
        {
            public GameManager GameManager;
            public CurrencyManager Currency;
            public SaveManager Save;
            public AdManager Ads;
            public AudioManager Audio;
            public AudioEventBinder AudioBinder;
            public PrestigeManager Prestige;
            public QuestManager Quests;
        }

        private sealed class StationRow
        {
            public Station Station;
            public TMP_Text Name;
            public TMP_Text Level;
            public TMP_Text Income;
            public TMP_Text Cost;
            public Button Button;
            public Image Progress;
        }

        private sealed class PrestigeRowParts
        {
            public TMP_Text Name;
            public TMP_Text Level;
            public TMP_Text Bonus;
            public TMP_Text Cost;
            public Button Buy;
        }

        private sealed class UiParts
        {
            public readonly List<StationRow> StationRows = new List<StationRow>();
            public TMP_Text CurrencyText;
            public TMP_Text IncomeText;
            public Button SettingsButton;
            public Button BoostButton;
            public TMP_Text BoostText;
            public Button PrestigeOpenButton;
            public TMP_Text ToastText;
            public GameObject OfflinePopup;
            public TMP_Text OfflineAmountText;
            public TMP_Text OfflineDurationText;
            public Button OfflineCollectButton;
            public Button OfflineDoubleButton;
            public TMP_Text OfflineDoubleButtonText;
        }

        /// <summary>Arayüz renkleri ve Unity'nin yerleşik UI sprite'ları.</summary>
        private static class UiStyle
        {
            public static readonly Color TopBar = new Color(0.12f, 0.09f, 0.08f, 0.55f);
            public static readonly Color Card = new Color(1f, 0.98f, 0.94f, 0.95f);
            public static readonly Color RowTint = new Color(0.96f, 0.91f, 0.84f, 1f);
            public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
            public static readonly Color DarkText = new Color(0.22f, 0.15f, 0.11f);
            public static readonly Color MutedText = new Color(0.45f, 0.38f, 0.33f);
            public static readonly Color SoftWhite = new Color(1f, 1f, 1f, 0.85f);
            public static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
            public static readonly Color Green = new Color(0.27f, 0.62f, 0.34f);
            public static readonly Color Orange = new Color(0.93f, 0.53f, 0.2f);
            public static readonly Color Purple = new Color(0.52f, 0.36f, 0.82f);
            public static readonly Color Gem = new Color(0.2f, 0.62f, 0.8f);
            public static readonly Color Red = new Color(0.82f, 0.3f, 0.28f);
            public static readonly Color Neutral = new Color(0.4f, 0.34f, 0.3f);
            public static readonly Color ProgressBack = new Color(0.86f, 0.8f, 0.72f);
            public static readonly Color ProgressFill = new Color(0.98f, 0.72f, 0.2f);

            /// <summary>9 dilimli yuvarlak köşeli yerleşik sprite (Image.Type.Filled de sprite ister).</summary>
            public static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            public static Sprite CheckmarkSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        }
    }

    /// <summary>Sahne üretiminin özeti.</summary>
    public sealed class SceneSetupResult
    {
        public SceneSetupResult(string scenePath, int createdAssets, int reusedAssets, GameManager gameManager, UIManager uiManager)
        {
            ScenePath = scenePath;
            CreatedAssets = createdAssets;
            ReusedAssets = reusedAssets;
            GameManager = gameManager;
            UIManager = uiManager;
        }

        public string ScenePath { get; }
        public int CreatedAssets { get; }
        public int ReusedAssets { get; }
        public GameManager GameManager { get; }
        public UIManager UIManager { get; }
    }
}
