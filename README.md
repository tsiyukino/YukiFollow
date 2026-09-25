# Yuki Follow

Keep objects on a mesh surface while its blendshapes change, and make outfits follow the body's blendshapes. Applied at build time through [NDMF](https://github.com/bdunderscore/ndmf); the scene and your assets are never changed.

[English](#english) · [中文](#中文) · [日本語](#日本語)

## English

### Install

1. Add the TsiYuki VPM listing to VCC / ALCOM: `https://tsiyukino.github.io/vpm-repos/index.json`
2. Add **Yuki Follow** to your project.

### Surface Anchor

Blendshapes deform a mesh but never move objects, so an SPS socket on a nipple or a name tag on a shirt stays where it was when the breasts get bigger. `TsiYuki/Yuki Surface Anchor` fixes that, like a vertex parent in Blender.

1. Put the object where it belongs on the mesh (e.g. the socket at the nipple) and add `TsiYuki/Yuki Surface Anchor` to it.
2. The nearest mesh is picked as the surface; change it if needed. Keep **Follow tilt** on for SPS sockets so the insertion direction follows too.
3. The inspector lists the blendshapes that move this point. Enter Play mode or upload: whatever animates those blendshapes (menus, wardrobe, Modular Avatar, VRCFury) moves the object with the surface.

In the editor the object does not move. If you change a blendshape slider in the scene after placing it, the Scene view shows (orange) where it will be at build time.

### Blendshape Sync

`TsiYuki/Yuki Blendshape Sync` (one per avatar) makes every mesh follow the body's blendshapes of the same name: an outfit with `Breasts_big` changes with the body, whatever animates the body. The body is found automatically (meshes named `Body…` under the avatar root). Shrink blendshapes, visemes and separator lines are skipped; add meshes or name patterns to exclude more. The inspector shows every mesh and blendshape that will follow.

## 中文

**Surface Anchor**：形态键只让网格变形，不会带动物体。比如乳头上的 SPS socket、衬衫上的名牌，胸部变大时会留在原地。把 `TsiYuki/Yuki Surface Anchor` 挂在物体上（先把物体放到网格上正确的位置），构建时（Play 模式或上传）无论形态键由菜单、衣柜、Modular Avatar 还是 VRCFury 驱动，物体都会跟着表面移动和倾斜，类似 Blender 的顶点父级。SPS socket 请保持“跟随倾斜”开启。放置后如果在场景里改了形态键，Scene 视图会用橙色标出构建时的位置。

**Blendshape Sync**：每个模型挂一个 `TsiYuki/Yuki Blendshape Sync`，所有网格都会跟随身体的同名形态键（场景数值和动画都同步）。自动识别模型根物体下名字以 `Body` 开头的网格为身体；默认排除 Shrink、口型和分隔线，也可以自己添加要排除的网格或名字。

## 日本語

**Surface Anchor**：シェイプキーはメッシュを変形させるだけで、オブジェクトは動かしません。乳首の SPS ソケットやシャツの名札は、胸が大きくなってもその場に残ります。オブジェクトをメッシュ上の位置に置いて `TsiYuki/Yuki Surface Anchor` を追加すると、ビルド時（Play モード・アップロード）に、シェイプキーを何が動かしていても（メニュー、衣装切り替え、Modular Avatar、VRCFury）表面に合わせて移動・傾きます。Blender の頂点ペアレントと同様です。SPS ソケットでは「傾きも追従」を有効のままにしてください。

**Blendshape Sync**：アバターに 1 つ `TsiYuki/Yuki Blendshape Sync` を追加すると、すべてのメッシュが素体の同名シェイプキーに追従します（シーンの値とアニメーションの両方）。素体はアバター直下の `Body…` という名前のメッシュから自動で選ばれます。Shrink 系・口の形・区切り線は既定で除外され、除外するメッシュや名前を追加できます。

## License

MIT
