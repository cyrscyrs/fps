

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-09-02 18:13:50] 用户在做末日丧尸 FPS；主关卡 = Assets/Scenes/SampleScene.unity（用 RPG_FPS_game_assets_industrial 资产包搭的 100x100m 工业废墟竞技场，含机库/集装箱掩体/油罐区/围栏/路障）。**Why:** 后续玩家控制、丧尸 AI、武器系统都围绕此场景与题材展开。**How to apply:** 改玩法功能时以此场景为准，别另起场景。
- [2026-09-02 18:13:50] RPG_FPS_game_assets_industrial 包的 .mat 材质原为 Mobile/Diffuse（不接收阴影，方向光怎么调都无影），2026-09-02 已批量转为 Standard（保留贴图、金属度0、光滑度0）；若重新导入该包材质会回退，需再次转换。另：包内 Dust/Smoke 粒子与 Lattice 材质保持原 shader 勿动。
- [2026-09-02 18:13:50] 本项目 exec_editor_script 改场景后 Unity 的 scene.dirty 可能误报 false（调用过 AssetDatabase.SaveAssets 后尤甚），ensure_scene_saved 会跳过实际写盘；必须用 EditorSceneManager.SaveScene 强制保存，并用 search_file_content 检查场景文件内容（如 "value: 对象名"）验证已持久化。

### Reference

