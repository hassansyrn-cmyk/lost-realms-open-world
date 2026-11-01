using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostRealmsOW {
 public readonly struct OWBlacksmithListing {
  public readonly int WeaponId;
  public readonly int GoldCost;
  public readonly int OreCost;

  public OWBlacksmithListing(int weaponId, int goldCost, int oreCost) {
   WeaponId = weaponId;
   GoldCost = goldCost;
   OreCost = oreCost;
  }
 }

 public static class OpenWorldWeaponShop {
  public const string BlacksmithNpcId = "brom";
  public static readonly Vector3 WorkshopAnchor = new Vector3(-18.5f, 0f, 9.2f);

  // Blacksmith forged weapon catalog
  public static readonly OWBlacksmithListing[] ForgedWeapons = {
   new OWBlacksmithListing(1, 40, 1),   // Dueling Blade (Slash)
   new OWBlacksmithListing(2, 50, 1),   // War Axe (Chop)
   new OWBlacksmithListing(16, 45, 1),  // Caravan Iron Sword (Slash)
   new OWBlacksmithListing(3, 75, 2),   // Battle Axe (Chop)
   new OWBlacksmithListing(4, 80, 2),   // Ranger Axe (Chop)
   new OWBlacksmithListing(5, 110, 3),  // Reaper's Spear (Spear)
   new OWBlacksmithListing(6, 160, 4),  // Crusher Hammer (Rare Ember)
   new OWBlacksmithListing(7, 190, 4),  // Frost Halberd (Rare Frost)
   new OWBlacksmithListing(8, 220, 5),  // Ornate Curved Blade (Rare Gale)
  };

  static GameObject workshopRoot;
  static Material[] workshopMaterials;
  static OpenWorldUITheme theme;
  static OpenWorldUITheme Theme => theme ?? (theme = new OpenWorldUITheme());

  public static void Tick(OpenWorldManager gm) {
   if (!gm || !gm.Hero || !gm.ActiveTerrain) return;
   if(!workshopRoot)foreach(Transform child in gm.WorldRoot)if(child.name=="Blacksmith_RoyalForge_Workshop"){if(!workshopRoot)workshopRoot=child.gameObject;else UnityEngine.Object.Destroy(child.gameObject);}
   if (!workshopRoot || workshopRoot.transform.parent != gm.WorldRoot) { if (workshopRoot) UnityEngine.Object.Destroy(workshopRoot); SpawnWorkshop(gm); }
   TickModal(gm);
  }

  public static bool OpenShop(OpenWorldManager gm) {
   if (!gm || !gm.Hero || gm.Hero.Dead || gm.IsModalOpen) return false;
   gm.ShowWeaponShopModal = true;
   OpenWorldAudio.Play("sfx_upgrade", 0.55f);
   return true;
  }

  public static void Open() {
   var gm = OpenWorldManager.I ?? UnityEngine.Object.FindAnyObjectByType<OpenWorldManager>();
   if (gm) OpenShop(gm);
  }

  static void SpawnWorkshop(OpenWorldManager gm) {
   if (workshopRoot) return;
   

   float groundY = OpenWorldMaterialHelper.SampleGroundHeight(WorkshopAnchor);
   Vector3 basePos = new Vector3(WorkshopAnchor.x, groundY, WorkshopAnchor.z);

   var shopRoot = new GameObject("Blacksmith_RoyalForge_Workshop");
   workshopRoot = shopRoot;
   shopRoot.transform.SetParent(gm.WorldRoot, false);
   shopRoot.transform.position = basePos;

   LoadMaterials();

   // 0. The Complete Authentic Blacksmith Workshop Building (Stone foundation, chimney, timber frame, hearth canopy)
   var bldg = SpawnProp("World/BlacksmithShop/Models/Blacksmith_Building",
    shopRoot.transform, basePos, Quaternion.Euler(0f, -90f, 0f), 1f);
   if (bldg) {
    foreach (var mf in bldg.GetComponentsInChildren<MeshFilter>(true)) {
     if (mf && mf.sharedMesh && !mf.GetComponent<Collider>()) {
      if (mf.sharedMesh.isReadable) {
       var mc = mf.gameObject.AddComponent<MeshCollider>();
       mc.sharedMesh = mf.sharedMesh;
      } else {
       // Android strips CPU mesh data: collision cooking needs a primitive fallback.
       var box = mf.gameObject.AddComponent<BoxCollider>();
       box.center = mf.sharedMesh.bounds.center;
       box.size = mf.sharedMesh.bounds.size;
      }
     }
    }
    foreach (var r in bldg.GetComponentsInChildren<Transform>(true)) {
     if (r && r.name.Contains("WeaponStand")) {
      AddShopTrigger(r.gameObject);
     }
    }
   } else {
    // Fallback individual prop arrangement
    SpawnProp("World/BlacksmithShop/Models/Blacksmith_Forge",
     shopRoot.transform, basePos + new Vector3(-2.2f, 0f, 0.4f), Quaternion.Euler(0f, 75f, 0f), 1f);
    SpawnProp("World/BlacksmithShop/Models/Blacksmith_Anvil",
     shopRoot.transform, basePos + new Vector3(0.6f, 0f, -0.6f), Quaternion.Euler(0f, -30f, 0f), 1f);
    var r1 = SpawnProp("World/BlacksmithShop/Models/Blacksmith_WeaponRack1",
     shopRoot.transform, basePos + new Vector3(1.9f, 0f, -1.2f), Quaternion.Euler(0f, -80f, 0f), 1f);
    if (r1) AddShopTrigger(r1);
    var r2 = SpawnProp("World/BlacksmithShop/Models/Blacksmith_WeaponRack2",
     shopRoot.transform, basePos + new Vector3(2.2f, 0f, 0.8f), Quaternion.Euler(0f, -100f, 0f), 1f);
    if (r2) AddShopTrigger(r2);
    SpawnProp("World/BlacksmithShop/Models/Blacksmith_Workbench",
     shopRoot.transform, basePos + new Vector3(-1.8f, 0f, -1.3f), Quaternion.Euler(0f, 20f, 0f), 1f);
   }

   var fireLightGo = new GameObject("ForgeFireLight");
   fireLightGo.transform.SetParent(shopRoot.transform, false);
   fireLightGo.transform.position = basePos + Quaternion.Euler(0,-90,0) * new Vector3(1.12f, 1.2f, 1.57f);
   var light = fireLightGo.AddComponent<Light>();
   light.type = LightType.Point;
   light.color = new Color(1.0f, 0.55f, 0.18f);
   light.range = 8.0f;
   light.intensity = 2.5f;

   // 6. Overhead Shop Billboard Banner
   var signObj = new GameObject("ForgeSignLabel");
   signObj.transform.SetParent(shopRoot.transform, false);
   signObj.transform.position = basePos + new Vector3(4.4f, 3.5f, 0f);
   signObj.transform.rotation=Quaternion.Euler(0,-90,0);
   var tm = signObj.AddComponent<TextMesh>();
   tm.text = "THE ROYAL FORGE\n<Master Smith Brom>";
   tm.fontSize = 32;
   tm.characterSize = 0.045f;
   tm.anchor = TextAnchor.MiddleCenter;
   tm.alignment = TextAlignment.Center;
   tm.color = new Color(1.0f, 0.86f, 0.44f);

   Debug.Log($"[BlacksmithShop] Royal Forge workshop spawned at {basePos.ToString("0.0")}");
  }

  public static bool TryForge(OpenWorldManager gm, int id) {
   if (!gm || !gm.Hero || gm.Hero.Dead || !gm.ShowWeaponShopModal || gm.UnlockedWeapons.Contains(id)) return false;
   int index = Array.FindIndex(ForgedWeapons, x => x.WeaponId == id);
   if (index < 0) return false;
   var item = ForgedWeapons[index];
   if (gm.Gold < item.GoldCost || gm.Ore < item.OreCost) return false;
   gm.UnlockedWeapons.Add(id); gm.Gold -= item.GoldCost; gm.Ore -= item.OreCost;
   gm.Hero.EquipWeapon(id); gm.MarkProgressDirty(); return true;
  }
  static void AddShopTrigger(GameObject prop) {
   var col = prop.GetComponent<Collider>() ?? prop.AddComponent<BoxCollider>();
   if (col is BoxCollider bc) {
    bc.size = new Vector3(1.5f, 2f, 1.5f);
    bc.center = new Vector3(0f, 1f, 0f);
    bc.isTrigger = true;
   }
   prop.AddComponent<OpenWorldWeaponRackTrigger>();
  }

  static GameObject SpawnProp(string resourcePath, Transform parent, Vector3 worldPos, Quaternion rot, float scale) {
   var prefab = Resources.Load<GameObject>(resourcePath);
   if (!prefab) {
    Debug.LogWarning($"[BlacksmithShop] Resource not found: {resourcePath}");
    return null;
   }
   var instance = UnityEngine.Object.Instantiate(prefab, worldPos, rot, parent);
   instance.transform.localScale = Vector3.one * scale;

   if (workshopMaterials != null && workshopMaterials.Length > 0) {
    var renderers = instance.GetComponentsInChildren<Renderer>(true);
    foreach (var r in renderers) {
     var mats = r.sharedMaterials;
     for (int i = 0; i < mats.Length; i++) {
      if (!mats[i]) continue;
      string mName = mats[i].name;
      foreach (var wm in workshopMaterials) {
       if (wm && (mName.Contains(wm.name) || wm.name.Contains(mName))) {
        mats[i] = wm;
        break;
       }
      }
     }
     r.sharedMaterials = mats;
    }
   }
   return instance;
  }

  static void LoadMaterials() {
   if (workshopMaterials != null) return;
   var matList = new List<Material>();
   var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

   var configs = new[] {
    new { Name = "WoodExteriorMat", BaseTex = "Image_0", NormTex = "Image_2" },
    new { Name = "WeaponsToolsMat", BaseTex = "Image_3", NormTex = "Image_5" },
    new { Name = "ForgeMat", BaseTex = "Image_6", NormTex = "Image_8" },
    new { Name = "blacksmithsMat", BaseTex = "Image_9", NormTex = "Image_11" },
    new { Name = "FurnitureMat", BaseTex = "Image_12", NormTex = "Image_14" },
    new { Name = "InteriorItemsMat", BaseTex = "Image_15", NormTex = "Image_17" }
   };

   foreach (var cfg in configs) {
    var mat = new Material(shader) { name = cfg.Name };
    var baseTex = Resources.Load<Texture2D>("World/BlacksmithShop/Textures/" + cfg.BaseTex);
    var normTex = Resources.Load<Texture2D>("World/BlacksmithShop/Textures/" + cfg.NormTex);

    if (baseTex) mat.SetTexture("_BaseMap", baseTex);
    if (normTex) {
     mat.SetTexture("_BumpMap", normTex);
     mat.EnableKeyword("_NORMALMAP");
    }
    mat.SetFloat("_Smoothness", 0.35f);
    matList.Add(mat);
   }
   workshopMaterials = matList.ToArray();
  }

  static GameObject modalRoot;
  static RectTransform rowsContainer;
  static Text currencyText;

  static void TickModal(OpenWorldManager gm) {
   if (!gm.ShowWeaponShopModal) {
    if (modalRoot && modalRoot.activeSelf) modalRoot.SetActive(false);
    return;
   }
   if (!modalRoot) {
    BuildModalUI(gm);
    RefreshModal(gm);
   }
   if (!modalRoot) return;
   if (!modalRoot.activeSelf) {
    modalRoot.SetActive(true);
    RefreshModal(gm);
   }
   if (currencyText) {
    currencyText.text = $"Gold: <color=#FFD54F>{gm.Gold}</color> G   •   Ore: <color=#90CAF9>{gm.Ore}</color>";
   }
  }

  static Font ResolveFont(bool heading) {
   var t = Theme;
   if (t != null) {
    var f = heading ? t.Heading : t.Body;
    if (f) return f;
   }
   return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  }

  static void BuildModalUI(OpenWorldManager gm) {
   var hud = OpenWorldMobileHud.Instance;
   if (!hud) return;

   var t = Theme;
   Font headFont = ResolveFont(true);
   Font bodyFont = ResolveFont(false);

   modalRoot = new GameObject("WeaponShopModal");
   modalRoot.transform.SetParent(hud.transform, false);

   var rootRect = modalRoot.AddComponent<RectTransform>();
   rootRect.anchorMin = Vector2.zero;
   rootRect.anchorMax = Vector2.one;
   rootRect.sizeDelta = Vector2.zero;

   // Dark backdrop
   var bg = modalRoot.AddComponent<Image>();
   bg.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);

   // Main Window Card
   var cardGo = new GameObject("ShopCard");
   cardGo.transform.SetParent(modalRoot.transform, false);
   var cardRect = cardGo.AddComponent<RectTransform>();
   cardRect.anchorMin = new Vector2(0.5f, 0.5f);
   cardRect.anchorMax = new Vector2(0.5f, 0.5f);
   cardRect.sizeDelta = new Vector2(780f, 640f);

   if (t != null) {
    t.Round(cardGo, new Color(0.06f, 0.09f, 0.16f, 0.95f), 18f, true);
   } else {
    var cardImg = cardGo.AddComponent<Image>();
    cardImg.color = new Color(0.06f, 0.09f, 0.16f, 0.95f);
   }

   // Header Title
   var titleGo = new GameObject("Title");
   titleGo.transform.SetParent(cardGo.transform, false);
   var titleRect = titleGo.AddComponent<RectTransform>();
   titleRect.anchorMin = new Vector2(0f, 1f);
   titleRect.anchorMax = new Vector2(1f, 1f);
   titleRect.anchoredPosition = new Vector2(0f, -28f);
   titleRect.sizeDelta = new Vector2(-40f, 38f);

   var titleText = titleGo.AddComponent<Text>();
   titleText.text = "THE ROYAL FORGE • MASTER SMITH BROM";
   titleText.font = headFont;
   titleText.fontSize = 22;
   titleText.alignment = TextAnchor.MiddleCenter;
   titleText.color = OpenWorldUITheme.Gold;

   // Subtitle
   var subGo = new GameObject("Subtitle");
   subGo.transform.SetParent(cardGo.transform, false);
   var subRect = subGo.AddComponent<RectTransform>();
   subRect.anchorMin = new Vector2(0f, 1f);
   subRect.anchorMax = new Vector2(1f, 1f);
   subRect.anchoredPosition = new Vector2(0f, -56f);
   subRect.sizeDelta = new Vector2(-40f, 22f);

   var subText = subGo.AddComponent<Text>();
   subText.text = "Forge weapons using Gold & gathered Ore. Vault and quest blades cannot be forged.";
   subText.font = bodyFont;
   subText.fontSize = 13;
   subText.alignment = TextAnchor.MiddleCenter;
   subText.color = new Color(0.72f, 0.80f, 0.90f);

   // Currency Bar
   var curGo = new GameObject("CurrencyBar");
   curGo.transform.SetParent(cardGo.transform, false);
   var curRect = curGo.AddComponent<RectTransform>();
   curRect.anchorMin = new Vector2(0f, 1f);
   curRect.anchorMax = new Vector2(1f, 1f);
   curRect.anchoredPosition = new Vector2(0f, -82f);
   curRect.sizeDelta = new Vector2(-40f, 24f);

   currencyText = curGo.AddComponent<Text>();
   currencyText.font = bodyFont;
   currencyText.fontSize = 15;
   currencyText.fontStyle = FontStyle.Bold;
   currencyText.alignment = TextAnchor.MiddleCenter;
   currencyText.color = Color.white;

   // Container for rows
   var rowsGo = new GameObject("RowsContainer");
   rowsGo.transform.SetParent(cardGo.transform, false);
   rowsContainer = rowsGo.AddComponent<RectTransform>();
   rowsContainer.anchorMin = new Vector2(0f, 0f);
   rowsContainer.anchorMax = new Vector2(1f, 1f);
   rowsContainer.anchoredPosition = new Vector2(0f, -44f);
   rowsContainer.sizeDelta = new Vector2(-44f, -156f);
   var viewport = rowsGo.AddComponent<RectMask2D>();
   var scroll = rowsGo.AddComponent<ScrollRect>();
   var content = new GameObject("Weapon list content", typeof(RectTransform));
   content.transform.SetParent(rowsGo.transform, false);
   rowsContainer = content.GetComponent<RectTransform>();
   rowsContainer.anchorMin = new Vector2(0f, 1f); rowsContainer.anchorMax = Vector2.one;
   rowsContainer.pivot = new Vector2(.5f, 1f); rowsContainer.anchoredPosition = Vector2.zero;
   scroll.viewport = rowsGo.GetComponent<RectTransform>(); scroll.content = rowsContainer;
   scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;

   // Close Button
   var closeGo = new GameObject("CloseButton");
   closeGo.transform.SetParent(cardGo.transform, false);
   var closeRect = closeGo.AddComponent<RectTransform>();
   closeRect.anchorMin = new Vector2(0.5f, 0f);
   closeRect.anchorMax = new Vector2(0.5f, 0f);
   closeRect.anchoredPosition = new Vector2(0f, 24f);
   closeRect.sizeDelta = new Vector2(160f, 36f);

   if (t != null) {
    t.Round(closeGo, new Color(0.12f, 0.16f, 0.25f, 0.95f), 12f);
   } else {
    var closeImg = closeGo.AddComponent<Image>();
    closeImg.color = new Color(0.12f, 0.16f, 0.25f);
   }

   var closeBtn = closeGo.AddComponent<Button>();
   closeBtn.onClick.AddListener(() => {
    gm.ShowWeaponShopModal = false;
    OpenWorldAudio.Play("sfx_click", 0.5f);
   });

   var closeTxtGo = new GameObject("Label");
   closeTxtGo.transform.SetParent(closeGo.transform, false);
   var closeTxtRect = closeTxtGo.AddComponent<RectTransform>();
   closeTxtRect.anchorMin = Vector2.zero;
   closeTxtRect.anchorMax = Vector2.one;
   closeTxtRect.sizeDelta = Vector2.zero;
   var closeTxt = closeTxtGo.AddComponent<Text>();
   closeTxt.text = "CLOSE";
   closeTxt.font = headFont;
   closeTxt.fontSize = 15;
   closeTxt.alignment = TextAnchor.MiddleCenter;
   closeTxt.color = OpenWorldUITheme.Gold;
  }

  public static void RefreshModal(OpenWorldManager gm) {
   if (!rowsContainer) return;

   var t = Theme;
   Font headFont = ResolveFont(true);
   Font bodyFont = ResolveFont(false);

   for (int i = rowsContainer.childCount - 1; i >= 0; i--) {
    UnityEngine.Object.Destroy(rowsContainer.GetChild(i).gameObject);
   }

   float rowH = 88f;
   rowsContainer.sizeDelta = new Vector2(0f, ForgedWeapons.Length * rowH);
   float startY = 0f;

   for (int i = 0; i < ForgedWeapons.Length; i++) {
    var item = ForgedWeapons[i];
    var def = OpenWorldWeaponCatalog.Get(item.WeaponId);
    bool unlocked = gm.UnlockedWeapons.Contains(item.WeaponId);
    bool equipped = gm.Hero && gm.Hero.EquippedWeaponId == item.WeaponId;
    bool canAfford = gm.Gold >= item.GoldCost && gm.Ore >= item.OreCost;

    var rowGo = new GameObject($"Row_{def.Name}");
    rowGo.transform.SetParent(rowsContainer, false);
    var rowRect = rowGo.AddComponent<RectTransform>();
    rowRect.anchorMin = new Vector2(0f, 1f);
    rowRect.anchorMax = new Vector2(1f, 1f);
    rowRect.anchoredPosition = new Vector2(0f, startY - i * rowH - 44f);
    rowRect.sizeDelta = new Vector2(0f, 82f);

    Color rowColor = equipped ? new Color(0.14f, 0.22f, 0.16f, 0.92f) :
      unlocked ? new Color(0.09f, 0.13f, 0.20f, 0.90f) :
      new Color(0.07f, 0.10f, 0.15f, 0.88f);

    if (t != null) {
     t.Round(rowGo, rowColor, 10f);
    } else {
     var rowImg = rowGo.AddComponent<Image>();
     rowImg.color = rowColor;
    }

    // Weapon Name & Style Icon
    string styleIcon = def.Style == "spear" ? "🔱" : def.Style == "chop" ? "🪓" : "⚔";
    string rarityColor = def.Rarity == OWWeaponRarity.Rare ? "#64B5F6" :
      def.Rarity == OWWeaponRarity.Epic ? "#BA68C8" : "#E0E0E0";

    // 2D Weapon Icon Card Preview (Zelda/Genshin inventory style)
    var iconBoxGo = new GameObject("IconBox");
    iconBoxGo.transform.SetParent(rowGo.transform, false);
    var iconBoxRect = iconBoxGo.AddComponent<RectTransform>();
    iconBoxRect.anchorMin = new Vector2(0f, 0.5f);
    iconBoxRect.anchorMax = new Vector2(0f, 0.5f);
    iconBoxRect.anchoredPosition = new Vector2(43f, 0f);
    iconBoxRect.sizeDelta = new Vector2(72f, 72f);

    Color iconBorderCol = def.Rarity == OWWeaponRarity.Rare ? new Color(0.25f, 0.50f, 0.85f, 0.95f) :
      def.Rarity == OWWeaponRarity.Epic ? new Color(0.65f, 0.35f, 0.85f, 0.95f) :
      new Color(0.20f, 0.28f, 0.36f, 0.95f);

    if (t != null) t.Round(iconBoxGo, iconBorderCol, 6f);
    else iconBoxGo.AddComponent<Image>().color = iconBorderCol;

    var iconImgGo = new GameObject("IconImg");
    iconImgGo.transform.SetParent(iconBoxGo.transform, false);
    var iconImgRect = iconImgGo.AddComponent<RectTransform>();
    iconImgRect.anchorMin = Vector2.zero;
    iconImgRect.anchorMax = Vector2.one;
    iconImgRect.sizeDelta = new Vector2(-4f, -4f);
    var iconImg = iconImgGo.AddComponent<Image>();

    var spr = Resources.Load<Sprite>($"UI/Icons/Weapons/Weapon_{item.WeaponId}");
    if (spr) {
     iconImg.sprite = spr;
     iconImg.preserveAspect = true;
     iconImg.color = Color.white;
    } else {
     var wTex = Resources.Load<Texture2D>($"UI/Icons/Weapons/Weapon_{item.WeaponId}");
     if (wTex) {
      iconImg.sprite = Sprite.Create(wTex, new Rect(0f, 0f, wTex.width, wTex.height), new Vector2(0.5f, 0.5f));
      iconImg.preserveAspect = true;
      iconImg.color = Color.white;
     } else {
      iconImg.color = Color.clear;
     }
    }

    var nameGo = new GameObject("Name");
    nameGo.transform.SetParent(rowGo.transform, false);
    var nameRect = nameGo.AddComponent<RectTransform>();
    nameRect.anchorMin = new Vector2(0f, 0.5f);
    nameRect.anchorMax = new Vector2(0f, 0.5f);
    nameRect.anchoredPosition = new Vector2(190f, 0f);
    nameRect.sizeDelta = new Vector2(205f, 68f);

    var nameTxt = nameGo.AddComponent<Text>();
    nameTxt.text = $"{styleIcon} <color={rarityColor}><b>{def.Name}</b></color>\n<size=11><color=#90A4AE>DMG {def.Damage:0.0}× • REACH {def.Reach:0.0}m</color></size>";
    nameTxt.font = bodyFont;
    nameTxt.fontSize = 12;
    nameTxt.alignment = TextAnchor.MiddleLeft;

    // Ability summary
    var infoGo = new GameObject("Info");
    infoGo.transform.SetParent(rowGo.transform, false);
    var infoRect = infoGo.AddComponent<RectTransform>();
    infoRect.anchorMin = new Vector2(0f, 0.5f);
    infoRect.anchorMax = new Vector2(0f, 0.5f);
    infoRect.anchoredPosition = new Vector2(390f, 0f);
    infoRect.sizeDelta = new Vector2(195f, 68f);

    var infoTxt = infoGo.AddComponent<Text>();
    infoTxt.text = $"<size=11><color=#CFD8DC>{def.Summary}</color></size>";
    infoTxt.font = bodyFont;
    infoTxt.fontSize = 11;
    infoTxt.alignment = TextAnchor.MiddleLeft;

    // Action button
    var btnGo = new GameObject("ActionBtn");
    btnGo.transform.SetParent(rowGo.transform, false);
    var btnRect = btnGo.AddComponent<RectTransform>();
    btnRect.anchorMin = new Vector2(1f, 0.5f);
    btnRect.anchorMax = new Vector2(1f, 0.5f);
    btnRect.anchoredPosition = new Vector2(-75f, 0f);
    btnRect.sizeDelta = new Vector2(130f, 30f);

    var btn = btnGo.AddComponent<Button>();

    var btnTxtGo = new GameObject("Label");
    btnTxtGo.transform.SetParent(btnGo.transform, false);
    var btnTxtRect = btnTxtGo.AddComponent<RectTransform>();
    btnTxtRect.anchorMin = Vector2.zero;
    btnTxtRect.anchorMax = Vector2.one;
    btnTxtRect.sizeDelta = Vector2.zero;
    var btnTxt = btnTxtGo.AddComponent<Text>();
    btnTxt.font = bodyFont;
    btnTxt.fontSize = 12;
    btnTxt.fontStyle = FontStyle.Bold;
    btnTxt.alignment = TextAnchor.MiddleCenter;

    int wId = item.WeaponId;

    if (equipped) {
     if (t != null) t.Round(btnGo, new Color(0.20f, 0.38f, 0.18f), 8f);
     else btnGo.AddComponent<Image>().color = new Color(0.20f, 0.38f, 0.18f);
     btnTxt.text = "EQUIPPED";
     btnTxt.color = new Color(0.85f, 1.0f, 0.85f);
     btn.interactable = false;
    } else if (unlocked) {
     if (t != null) t.Round(btnGo, new Color(0.16f, 0.36f, 0.58f), 8f);
     else btnGo.AddComponent<Image>().color = new Color(0.16f, 0.36f, 0.58f);
     btnTxt.text = "EQUIP";
     btnTxt.color = Color.white;
     btn.onClick.AddListener(() => {
      gm.Hero.EquipWeapon(wId);
      OpenWorldAudio.Play("sfx_click", 0.5f);
      gm.ShowBanner($"EQUIPPED {def.Name}");
      RefreshModal(gm);
     });
    } else {
     if (canAfford) {
      if (t != null) t.Round(btnGo, new Color(0.72f, 0.50f, 0.12f), 8f);
      else btnGo.AddComponent<Image>().color = new Color(0.72f, 0.50f, 0.12f);
      btnTxt.text = $"FORGE ({item.GoldCost}G {item.OreCost}O)";
      btnTxt.color = Color.white;
      btn.onClick.AddListener(() => {
       if (!TryForge(gm, wId)) return;
       btn.interactable = false;
       gm.MarkProgressDirty();
       OpenWorldAudio.Play("sfx_upgrade", 0.6f);
       gm.SpawnBurst(gm.Hero.transform.position + Vector3.up, new Color(1f, 0.85f, 0.3f), 20);
       gm.ShowBanner($"FORGED {def.Name}! Added to Arsenal (−{item.GoldCost}G, −{item.OreCost} Ore)");
       RefreshModal(gm);
      });
     } else {
      if (t != null) t.Round(btnGo, new Color(0.20f, 0.22f, 0.28f, 0.7f), 8f);
      else btnGo.AddComponent<Image>().color = new Color(0.20f, 0.22f, 0.28f, 0.7f);
      btnTxt.text = $"{item.GoldCost}G • {item.OreCost}O";
      btnTxt.color = new Color(0.65f, 0.65f, 0.70f);
      btn.interactable = false;
     }
    }
   }
  }
 }

 public sealed class OpenWorldWeaponRackTrigger : MonoBehaviour {
  void OnTriggerEnter(Collider other) {
   var gm = OpenWorldManager.I;
   if (!gm || !other.CompareTag("Player")) return;
  }
 }

 public static class OpenWorldWeaponShopDebug {
  public static void Open() {
   var gm = OpenWorldManager.I;
   if (!gm) { Debug.LogError("[WeaponShop] OpenWorldManager missing"); return; }
   OpenWorldWeaponShop.OpenShop(gm);
   Debug.Log($"[WeaponShop] modal open={gm.ShowWeaponShopModal}, Gold={gm.Gold}, Ore={gm.Ore}");
  }

  public static void Buy(string weaponIdStr) {
   var gm = OpenWorldManager.I;
   if (!gm) return;
   if (!int.TryParse(weaponIdStr, out int wId)) return;
   var item = Array.Find(OpenWorldWeaponShop.ForgedWeapons, x => x.WeaponId == wId);
   if (item.WeaponId == 0) return;

   if (gm.Gold < item.GoldCost) gm.AddGold(item.GoldCost);
   if (gm.Ore < item.OreCost) gm.Ore += item.OreCost;

   gm.Gold -= item.GoldCost;
   gm.Ore -= item.OreCost;
   gm.UnlockedWeapons.Add(wId);
   gm.Hero.EquipWeapon(wId);
   gm.MarkProgressDirty();
   Debug.Log($"[WeaponShop] Bought weapon {wId} ({OpenWorldWeaponCatalog.Get(wId).Name}). Unlocked count={gm.UnlockedWeapons.Count}");
  }

  public static void Validate() {
   var gm = OpenWorldManager.I;
   if (!gm) throw new Exception("OpenWorldManager missing");
   if (!gm.Hero) throw new Exception("Hero missing");
   var brom = gm.NPCs.Find(n => n && n.NpcId == OpenWorldWeaponShop.BlacksmithNpcId);
   if (!brom) throw new Exception("Master Smith Brom NPC missing");
   Debug.Log($"[WeaponShop] Validate OK: Master Smith Brom present, Gold={gm.Gold}, Ore={gm.Ore}, Unlocked={gm.UnlockedWeapons.Count}");
  }
 }
}
