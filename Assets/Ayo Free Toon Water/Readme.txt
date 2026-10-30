======================================================================
[Documentation] Free Toon Water - Prefab & Shader Guide
======================================================================

Thank you for importing this water asset. This package provides pre-configured 
materials and prefabs to quickly set up a stylized cartoon water surface in your scene.

感謝您匯入此水面資源。本文件包含預先設定好的材質球與 Prefab，幫助您在場景中快速建立日系卡通風格的水面效果。

----------------------------------------------------------------------
1. PACKAGE CONTENTS / 資源包內容說明
----------------------------------------------------------------------
Inside the asset folder, you will find:
（在資源資料夾內，您將會找到：）

- /Materials: Contains the pre-configured water material using the core shader.
  （材質球資料夾：包含已預先套用核心 Shader 且參數為預設值的水面材質球。）
- /Meshes: Contains the custom "Water Plane" model with higher vertex density for waves.
  （網格資料夾：包含專為波浪起伏設計、擁有稍高面數的自訂 "Water Plane" 平面模型。）
- /Prefabs: Ready-to-use water planes with materials attached.
  （預製物件資料夾：已綁定好材質球、可直接拖放至場景使用的水面 Prefab。）
- /Shaders: Contains the core "Ayo_ToonWater" shader graph/code.
  （著色器資料夾：包含核心的 "Ayo_ToonWater" 卡通水面著色器。）
- /Demo: Contains the showcase scene and its texturing assets. Double-click the Demo Scene inside to view the setup.
  （範例資料夾：包含展示場景與其所需的貼圖資源。雙擊資料夾內的 Demo Scene 即可直接觀看效果。）

----------------------------------------------------------------------
2. SHADER FEATURES / 著色器功能特色
----------------------------------------------------------------------
- Highly Customizable Colors: Easily tweak shallow and deep water color gradients.
  （高度自訂顏色：可自由調整深水區與淺水區的漸層色彩。）
- Underwater Refraction: Realistic stylized screen-space distortion for submerged objects.
  （水底折射效果：提供水面下物體的日系風格畫面扭曲折射表現。）
- Caustics & Wave Highlights: Dynamic underwater light patterns and surface wave highlights.
  （水底焦散與波面高光：包含動態水底焦散光影與水面波紋的高光細節。）
- Specular Sun Reflection: Vibrant stylized glare reflecting directly from directional light sources.
  （陽光鏡面反射：完美呈現來自方向光源的日系卡通風格太陽反光。）
- Shoreline Foam Blending: Dynamic foam generation where the water intersects with 3D geometry.
  （動態岸邊泡沫：水面與 3D 物件交界處會自動產生交融的卡通泡沫邊緣。）
- Animated Waves: Smooth and lightweight surface ripple movement.
  （動態波浪效果：流暢且輕量化的表面漣漪動態。）

----------------------------------------------------------------------
3. ADVANCED KEY FEATURES / 核心獨家特點
----------------------------------------------------------------------
- Unity Toon Shader (UTS) Outline Compatibility:
  When objects on the water surface use UTS with custom outlines, the character body will NOT be distorted by the underwater refraction. The outline rendering can be fine-tuned via the "ReFraction Edge Tolerance" property. (Recommended value: 5 to 10).
  （相容 Unity Toon Shader 外描邊）：
  當水面上的物件使用 Unity Toon Shader (UTS) 的外描邊時，角色本體不會跟著折射效果產生不自然的扭曲。外描邊的邊緣表現可透過 "ReFraction Edge Tolerance" 參數來進行微調，建議設定數值在 5 ~ 10 之間。

- Physically-Based Shadow Rendering:
  In the Demo scene, a rooftop has been placed over a corner of the pool to showcase the shader's advanced light-shadow integration. The water reacts accurately to shadows, avoiding the common issue of global specular glare or identical shading under direct sunlight vs. covered shadow areas.
  （陰影下的光學物理表現）：
  Demo 場景特別在水池的一個角落蓋了屋頂，目的是展示水體在陰影下也能符合光學物理的正常表現。本 Shader 解決了傳統卡通 Shader「全域皆發生高光」或「陽光下與陰影下呈現完全相同顏色」的通病，提供更流暢的陰影遮蔽過渡。

----------------------------------------------------------------------
4. HOW TO USE & OPTIMIZE / 使用步驟與優化提示
----------------------------------------------------------------------
To apply the toon water to your own scene, please read the following guidelines:
若要在您自己的場景中使用此卡通水面，請參考以下使用指引：

1. Quick Start:
   Go to the "/Prefabs" folder and drag the water prefab directly into your Hierarchy. 
   This prefab is built using our custom "Water Plane" model for the best wave results.
   （快速開始：前往 "/Prefabs" 資料夾，將水面 Prefab 直接拖進場景。此 Prefab 已使用自訂模型優化。）

2. Mesh & Wave Performance (Important):
   - We highly recommend using the included "Water Plane" mesh from the "/Meshes" folder. 
   - You can replace it with Unity's default plane (GameObject > 3D Object > Plane), but please note that default planes have lower vertex density, which results in poorer wave vertex-displacement rendering.
   - IMPORTANT: Remember to REMOVE the "Mesh Collider" from your water object to avoid any unwanted physics calculations or visual collision glitches.
   （網格與波浪效果提示）：
   - 強烈建議使用 "/Meshes" 資料夾內的 "Water Plane" 模型，此模型具備稍高的面數，能完美呈現水面高低起伏的波浪效果。
   - 您雖然可以用 Unity 內建的 Plane 取代，但內建平面面數較低，模擬高低波浪的效果會比較差。
   - 【重要】請務必移除水面物件上的 "Mesh Collider"（網格碰撞器），以避免非預期的物理計算或碰撞問題。

3. URP Pipeline Settings Note:
   - In Unity 6 or higher, new URP projects enable "Depth Texture" and "Opaque Texture" by default. 
   - If the shoreline foam or underwater refraction does not render correctly in your project, please verify that your active Universal Render Pipeline Asset (URP Asset) has BOTH "Depth Texture" and "Opaque Texture" enabled in the Inspector.
   （URP 管線設定提示）：
   - 在 Unity 6 或更高版本中，新建的 URP 專案預設已勾選「深度圖」與「不透明貼圖」。
   - 若您的專案中岸邊泡沫或水底折射未能正常渲染，請檢查您目前啟用的 Universal Render Pipeline Asset（URP 設定檔），並確保在 Inspector 視窗中已勾選 "Depth Texture" 與 "Opaque Texture" 兩個選項。

----------------------------------------------------------------------
5. TECHNICAL SPECIFICATIONS / 技術規格
----------------------------------------------------------------------
- Unity Version: Created and tested in Unity 6.0 LTS (6.0.77f1)
- Render Pipeline: Universal Render Pipeline (URP) Exclusive
- Shader Name: Ayo_ToonWater (Shader Graph)

----------------------------------------------------------------------
6. TECHNICAL TUTORIAL & BREAKDOWN / 技術教學與製作解密
----------------------------------------------------------------------
For developers interested in learning how this cartoon water shader graph was 
built from scratch, please refer to the specific video breakdowns below:

*Note: The audio in the videos is in Chinese, and Traditional Chinese CC captions 
are enabled by default. Non-Chinese speakers can easily use YouTube's 
auto-translate feature to translate the captions into their native language.*

如果您想了解此卡通水面著色器的底層邏輯與 Shader Graph 從零建置步驟，歡迎參考以下各部分的詳細影片教學連結。

🎬 PART 1: Color Gradients, Foam, and Refraction
   (水 的深淺顏色變化、泡沫、折射這三個效果是如何做出來的？)
👉 https://youtu.be/TSnjX61g_0o

🎬 PART 2: UTS Character & Outline Refraction Fix
   (我是如何解決角色本體與外描邊不受水的折射效果影響？)
👉 https://youtu.be/0OcxhD0fLpM

🎬 PART 3: Underwater Caustics, Wave Highlights, and Physically-Based Unlit Shader
   (水底焦散+波面高光是怎麼做出來的？另外如何讓Unlit Shader Graph也能符合光學物理的正常表現？)
👉 https://youtu.be/Sz8C45J3OKc

🎬 PART 4: Specular Sun Reflection and Vertex-Displacement Waves
   (陽光鏡面反射與高低起伏的波浪效果是怎麼做出來的？)
👉 https://youtu.be/I_qBUs3vNX8

----------------------------------------------------------------------
7. SUPPORT / 聯絡資訊
----------------------------------------------------------------------
If you find this asset helpful, please consider leaving a review on the Asset Store. Your feedback is highly appreciated!
如果您覺得這個資源有幫助，歡迎在 Asset Store 上留下評價，您的反饋對我非常重要！

Support Email: yuwechang0311@gmail.com

======================================================================
