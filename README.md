# Yuki Follow

Keep objects on a mesh surface while its blendshapes change, make outfits follow the body's blendshapes, and make separate body parts shade like the body at their seam. Applied at build time through [NDMF](https://github.com/bdunderscore/ndmf); the scene and your assets are never changed.

[English](#english) · [中文](#中文) · [日本語](#日本語)

## English

### Install

1. Add the TsiYuki VPM listing to VCC / ALCOM: `https://tsiyukino.github.io/vpm-repos/index.json`
2. Add **Yuki Follow** to your project.

All three components are in the Hierarchy right-click menu under **TsiYuki > Follow**, and under **Add Component > TsiYuki > Follow**. The right-click entries act on the whole selection; **Blendshape Sync** goes on the avatar root, or selects the one the avatar already has.

### Surface Anchor

Blendshapes deform a mesh but never move objects, so an SPS socket on a nipple or a name tag on a shirt stays where it was when the breasts get bigger. `TsiYuki/Follow/Yuki Surface Anchor` fixes that, like a vertex parent in Blender.

1. Put the object where it belongs on the mesh (e.g. the socket at the nipple) and add `TsiYuki/Follow/Yuki Surface Anchor` to it.
2. The nearest mesh is picked as the surface; change it if needed. Keep **Follow tilt** on for SPS sockets so the insertion direction follows too.
3. The inspector lists the blendshapes that move this point. Enter Play mode or upload: whatever animates those blendshapes (menus, wardrobe, Modular Avatar, VRCFury) moves the object with the surface.

In the editor the object does not move. If you change a blendshape slider in the scene after placing it, the Scene view shows (orange) where it will be at build time.

### Blendshape Sync

`TsiYuki/Follow/Yuki Blendshape Sync` (one per avatar) makes every mesh follow the body's blendshapes of the same name: an outfit with `Breasts_big` changes with the body, whatever animates the body. The body is found automatically (meshes named `Body…` under the avatar root). Shrink blendshapes, visemes and separator lines are skipped; add meshes or name patterns to exclude more. The inspector shows every mesh and blendshape that will follow.

### Seam Normals

A body part that ships as its own mesh and sits in a hole of the body keeps its author's normals, so in some worlds its edge shows as a colour step even when the textures match. Add `TsiYuki/Follow/Yuki Seam Normals` to the part's mesh and set the body as the surface. At build time the normals along the seam take the body's and fade back to the part's own over the blend width (8 mm by default), measured along the part so its own detail keeps its shading. The inspector shows how many vertices change and by how much; the Scene view marks the seam. Give the part the same probe anchor as the body too (Modular Avatar Mesh Settings), or the two still light differently in worlds with light probes.

## 中文

三个组件都在 Hierarchy 右键菜单的 **TsiYuki > Follow** 下，也在 **Add Component > TsiYuki > Follow** 里。右键菜单会作用于所有选中的物体；**Blendshape Sync** 会加到模型根物体上，模型已有时则直接选中它。

**Surface Anchor**：形态键只让网格变形，不会带动物体。比如乳头上的 SPS socket、衬衫上的名牌，胸部变大时会留在原地。把 `TsiYuki/Follow/Yuki Surface Anchor` 挂在物体上（先把物体放到网格上正确的位置），构建时（Play 模式或上传）无论形态键由菜单、衣柜、Modular Avatar 还是 VRCFury 驱动，物体都会跟着表面移动和倾斜，类似 Blender 的顶点父级。SPS socket 请保持“跟随倾斜”开启。放置后如果在场景里改了形态键，Scene 视图会用橙色标出构建时的位置。

**Blendshape Sync**：每个模型挂一个 `TsiYuki/Follow/Yuki Blendshape Sync`，所有网格都会跟随身体的同名形态键（场景数值和动画都同步）。自动识别模型根物体下名字以 `Body` 开头的网格为身体；默认排除 Shrink、口型和分隔线，也可以自己添加要排除的网格或名字。

**Seam Normals**：作为独立网格、嵌在身体开口里的部件（比如替换身体某处的网格）保留着作者的法线，即使贴图一致，在某些地图里边缘也会显出色差。在部件的网格上挂 `TsiYuki/Follow/Yuki Seam Normals`，把身体设为过渡到的网格。构建时接缝处的法线会换成身体的法线，并在过渡宽度（默认 8 mm，沿部件表面测量）内逐渐回到部件自己的法线，部件本身的细节保持原来的明暗。Inspector 会显示改动的顶点数和角度，Scene 视图会标出接缝。另外请让部件和身体使用同一个光照探针锚点（Modular Avatar Mesh Settings），否则在有光照探针的地图里两者仍会受光不同。

## 日本語

3 つのコンポーネントは Hierarchy の右クリックメニュー **TsiYuki > Follow** と、**Add Component > TsiYuki > Follow** にあります。右クリックメニューは選択中のすべてのオブジェクトに適用されます。**Blendshape Sync** はアバターのルートに追加され、すでにある場合はそれを選択します。

**Surface Anchor**：シェイプキーはメッシュを変形させるだけで、オブジェクトは動かしません。乳首の SPS ソケットやシャツの名札は、胸が大きくなってもその場に残ります。オブジェクトをメッシュ上の位置に置いて `TsiYuki/Follow/Yuki Surface Anchor` を追加すると、ビルド時（Play モード・アップロード）に、シェイプキーを何が動かしていても（メニュー、衣装切り替え、Modular Avatar、VRCFury）表面に合わせて移動・傾きます。Blender の頂点ペアレントと同様です。SPS ソケットでは「傾きも追従」を有効のままにしてください。

**Blendshape Sync**：アバターに 1 つ `TsiYuki/Follow/Yuki Blendshape Sync` を追加すると、すべてのメッシュが素体の同名シェイプキーに追従します（シーンの値とアニメーションの両方）。素体はアバター直下の `Body…` という名前のメッシュから自動で選ばれます。Shrink 系・口の形・区切り線は既定で除外され、除外するメッシュや名前を追加できます。

**Seam Normals**：素体の穴に収まる別メッシュのパーツ（素体の一部を置き換えるメッシュなど）は作者の法線を持つため、テクスチャが合っていてもワールドによっては縁に色の段差が見えます。パーツのメッシュに `TsiYuki/Follow/Yuki Seam Normals` を追加し、合わせるメッシュに素体を指定すると、ビルド時に継ぎ目の法線を素体の法線に置き換え、なじませ幅（既定 8 mm、パーツの表面に沿って測定）でパーツ自身の法線に戻します。パーツ自体の細部は元の陰影のままです。インスペクターに変わる頂点数と角度が表示され、Scene ビューに継ぎ目が表示されます。ライトプローブのあるワールドで受光がずれないよう、パーツと素体のプローブアンカーも揃えてください（Modular Avatar Mesh Settings）。

## License

MIT
