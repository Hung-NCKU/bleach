# Source 模型轉入 Unity：艾斯・諾特

來源：[Äs Nödt (Bleach - BBS)](https://steamcommunity.com/sharedfiles/filedetails/?id=3173466403)，上傳者 Madness Overload。來源頁將模型與貼圖歸功於《BLEACH Brave Souls》開發者。記錄來源不等於宣稱模型開源；原作與遊戲資產權利仍屬原權利人。

1. 以 Valve 官方 SteamCMD 匿名下載公開 Workshop 項目：`+login anonymous +workshop_download_item 4000 3173466403 +quit`。未登入使用者帳號、未訂閱、未執行 addon 的 Lua。
2. 執行 `python Tools/SourceModels/extract_gma.py 原始.gma 輸出資料夾`，解開 MDL、VVD、VTX 與 VTF。使用 [Crowbar 0.74](https://github.com/ZeqMacaw/Crowbar/releases/tag/v0.74) 產生 reference SMD，沒有載入 Half-Life 共用動畫。
3. 安裝 `Tools/SourceModels/requirements.txt`，執行 `python Tools/SourceModels/decode_vtf.py 原始.vtf 輸出.png`，使用 [srctools 2.7.0](https://srctools.readthedocs.io/en/latest/modules/vtf.html) 解碼為 PNG。選用 `as_diffuse` 與 `as_face_1`，保留貼圖像素。
4. Unity 執行 `SourceCharacterBuild.ImportAsNodt`。它讀取保存在版本庫的 `as_nodt.smd` 與 `bracelets.smd`，轉換左右手座標、Z-up 與英吋單位，保留骨架階層、頂點、UV、材質分組及蒙皮權重，產生原生 Mesh 與 source prefab。
5. 執行 `RosterCharacterBuild.ValidateAndBuild`，對應 ValveBiped 骨名至遊戲關節、正規化尺寸、調整朝向及垂臂基準，輸出角色與場景預覽並建置 Windows player。
6. player 加 `-kido-smoke`，確認角色載入、來源資源存活、頭部留在角色站位、全部主線流程。另查看實際對戰截圖。

來源作者提醒 thorns / mantle bodygroups 可能損壞，因此本輪採用 body 與 bracelets，不匯入這兩個附加 bodygroup，也不複製來源 Lua、音效或配音。

直接檢視新角色：根目錄 `START-ASNODT.cmd`。通用入口是 `KidoTraining.exe -kido-character asnodt`；角色 ID 必須已有下載模型，無效 ID 不建立對戰。

## 本輪其餘候選的核對

- Sketchfab 的 `Isane (broken)`（caa4eacc0bf842a7a43aa05fbb28946f）：下載檔材質為 Fieldking / FieldsKing、EyeBallCreepy，頁面未提供 BLEACH 出處。無法確認是虎徹勇音，未匯入，不計入完成數。
- Steam 的 Bleach SNPCs（3471269316）：雖有大量角色，其作者明確禁止重新使用或重傳其移植內容；未下載／匯入該整合包。
- 友哈巴赫的 Steam 公開模型搜尋沒有結果；搜尋到的其他商品主要為列印雕像或付費 mod，未購買或繞過付費存取。
- 舊 BBS database 工具的 GitHub 連結目前回傳 404，未用不存在的工具作為已取得模型的依據。
