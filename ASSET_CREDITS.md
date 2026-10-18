# 素材來源與非營利研究標示

本專案以非營利學習、研究及同人製作為目的。本標示不是第三方素材的授權聲明，也不表示專案已取得官方認可。

## 目前實際包含的素材

- 角色初版、場景初版、卡通材質與合成音效：本專案的程序化實作。
- 語音辨識：Vosk 與中文模型；漢字讀音資料：pinyin-data。原有來源與授權說明見 [README](README.md#第三方資源)。
- BLEACH 的角色、作品名稱與世界觀權利屬其各自權利人；本專案沒有因此取得官方模型、OST 或配音的再散布授權。

**已實際匯入 ambientCG 的兩張 CC0 場景色彩貼圖，以及下列下載夜一模型；沒有匯入官方 OST 或官方角色語音。**

## 參考來源與候選素材

以下是調查紀錄，不是「已安裝素材」清單。機器可讀版本見 [sources.json](docs/asset-sources/sources.json)。

| 類別 | 來源／作者 | 用途與現況 |
| --- | --- | --- |
| 3D 模型 | [The Models Resource：Brave Souls](https://models.spriters-resource.com/pc_computer/bleachbravesouls/) | 參考目錄；不將目錄視為所有角色均有可用模型 |
| 一護模型 | [Ichigo / Boxy](https://models.spriters-resource.com/pc_computer/bleachbravesouls/asset/356572/) | 頁面列有模型、貼圖與動畫；下載遭網站安全驗證阻擋，未取得檔案 |
| 自製露琪亞模型 | [Rukia Kuchiki full body / Lappy](https://sketchfab.com/3d-models/rukia-kuchiki-full-body-2e3b80322bb04a8ea43626ddbcf3664f) | 頁面標示 CC BY 4.0；便服、未綁骨，下載需要登入，未取得檔案 |
| 角色語音 | [The Sounds Resource：Brave Souls](https://sounds.spriters-resource.com/mobile/bleachbravesouls/) | 已確認語音項目目錄；沒有取得公開再散布授權或複製語音包 |
| BGM | [KLab 官方音樂試聽](https://www.bleach-bravesouls.com/movie/) | 曲目與風格參考；未複製官方錄音 |
| OST | [Aniplex 官方音樂商品](https://www.aniplex.co.jp/lineup/bleach-anime/products/) | 唱片與原聲帶資訊參考；不是遊戲再利用授權 |
| 2D／歷代遊戲資料 | [Sprite Database：Dark Souls](https://spritedatabase.net/game/3) | 目錄參考；未匯入素材 |

The Models Resource 的[使用條款](https://models.spriters-resource.com/page/tou/)區分素材使用與原格式轉載，並列有非商業、未公開作品等條件，不能以來源連結代替逐項確認。Sketchfab 的 [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)要求署名、提供授權連結及標明修改；模型作者的標示也不代表其能授出全部底層角色權利。

未來真正匯入的每個檔案應記錄：名稱、作者／權利人、來源 URL、授權或許可依據、修改內容、檔案校驗碼，以及可否公開散布。新增的兩張通用貼圖依 CC0 使用；其他候選維持未匯入狀態。

調查日期：2026-10-18。

## 已使用的下載角色

**Cha Yoruichi** — [anywhere 3D](https://sketchfab.com/stephanua.gr)，
[原頁](https://sketchfab.com/3d-models/cha-yoruichi-3013541bdafb477ea8610bb1cda74128)。
Sketchfab 公開 API 標示 [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)。
透過 [AllenAI Objaverse](https://huggingface.co/datasets/allenai/objaverse) 的公開物件鏡像取得。
資料集為 ODC-By 1.0，個別模型沿用原作者 CC 授權。
此模型不是本專案原創；角色權利仍屬各自權利人。

原始 GLB 位於 `Assets/ImportedCharacters/Source/Yoruichi.glb`，位元組未修改。
適配內容：縮放與原點、卡通材質、48 根原骨骼中的 11 個關節對應遊戲姿勢。
選角、頭像與戰鬥中的夜一均使用此模型。來源及 SHA-256 見
[yoruichi-model.json](docs/asset-sources/yoruichi-model.json)。

匯入器 [glTFast v5.0.4 / Andreas Atteneder](https://github.com/atteneder/glTFast/tree/v5.0.4)
採 Apache-2.0，授權保留於 `ThirdPartyNotices/glTFast/LICENSE.md`。

公開／開源候選與採用順序見 [open-resources.md](docs/asset-sources/open-resources.md)，涵蓋動畫、材質、卡通渲染與 BLEACH 資料庫；候選未視為已匯入素材。
# 本輪角色模型整合

已使用 21 名下載角色的骨架模型與貼圖，完整進度見
[角色替換清單](docs/art-remake/ROSTER-REPLACEMENT.md)。
Models Resource 的 19 名來源及 TsuShi 的吉良模型逐項列於
[來源、署名與 SHA-256](docs/asset-sources/roster-models.json)。
夜一的 CC BY 4.0 來源另列於
[夜一模型紀錄](docs/asset-sources/yoruichi-model.json)。
上述既有遊戲／角色資產沒有被改稱為專案原創、CC0 或開源作品。
