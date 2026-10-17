# 公開／開源素材調查（2026-10-18）

後續整合更新：已匯入兩張 ambientCG CC0 場景貼圖，見 [imported-assets.json](imported-assets.json) 與 [主線更新紀錄](../art-remake/story-update.md)。下文保留調查當時候選狀態。

結論：有可加速製作的開放資源；本次尚未找到經確認可整包再散布的 BLEACH 官方角色、場景、OST 與配音套件。公開儲存庫、可下載及開放授權是不同條件。本清單僅記錄候選，尚未匯入或測試相容性。

| 資源 | 已確認內容與授權 | 我們的用途／限制 |
| --- | --- | --- |
| [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html) | 官方頁標示 CC0，120+ 人形動畫，FBX/GLB/Blend 與 Unity 重定向支援資訊 | 優先評估移動、受擊、倒地；現有程序骨架不是 Humanoid，需做骨架與重定向適配。不能直接變成斬魄刀專屬動作；免費包與付費 Source 版本須分清 |
| [Kenney](https://kenney.nl/assets)／[授權說明](https://kenney.nl/support) | 素材頁的遊戲素材 CC0 | UI、通用音效、道具；不含 BLEACH 人物。逐包保留 license |
| [Poly Haven](https://polyhaven.com/license) | 站內模型、貼圖、HDRI 為 CC0，可再散布 | 石材、木材、地面及天空；需經風格化處理，不能直接等同瀞靈廷場景 |
| [ambientCG](https://ambientcg.com/) | 官方頁標示素材 CC0 | 瓦片、布料、沙地與岩石的材質底稿；控制貼圖尺寸與色階 |
| [MToon](https://github.com/Santarh/MToon)／[LICENSE](https://github.com/Santarh/MToon/blob/master/LICENSE) | MIT，Masataka SUMI；須保留著作權與授權通知 | Unity 卡通陰影與輪廓技術候選；不是角色模型，也尚未整合測試 |
| [Resource of Souls](https://github.com/adaptajoe/resource-of-souls) | BLEACH Rebirth of Souls 民間 Wiki，根目錄 CC0-1.0；含角色 JSON 與遊戲動畫影片目錄 | 優先參考資料結構、招式分類與操作特性；其遊戲錄影、角色圖與台詞須與作者自行創作內容分別判斷，不推論全部官方媒體因此開放 |
| [OpenGameArt CC0 Music 收藏](https://opengameart.org/content/cc0-music) | 社群收藏入口，並非單一素材授權檔 | 可找原創 BGM 候選；必須逐曲開啟作者頁核對授權，收藏名稱不作為匯入依據 |
| [同人完全虛化一護 / asifsaj](https://sketchfab.com/3d-models/fully-hollowfied-ichigo-22fc6742f1f344f187d28d4cc0cb1842) | 搜尋索引顯示可下載與 CC Attribution；直接頁面本次讀取失敗 | 僅低信心線索，待確認作者、授權版本、綁骨、下載內容與角色底層權利，尚未取得檔案 |
| [同人露琪亞 / Lappy](https://sketchfab.com/3d-models/rukia-kuchiki-full-body-2e3b80322bb04a8ea43626ddbcf3664f) | 先前確認頁面標 CC BY 4.0，便服、未綁骨、需登入下載 | 不是即用戰鬥角色；尚未取得檔案，保留角色權利範圍未確認狀態 |

## Pokémon 類比

[pret/pokeemerald](https://github.com/pret/pokeemerald) 自述是 Pokémon Emerald 的反編譯專案，不能由「GitHub 上有完整原始資料」推論任天堂把角色圖與音樂以 CC0/MIT 釋出。我們可以學其資料驅動關卡、角色和資產管理方式。

## 對現有遊戲的採用順序

1. 通用動畫最先解決動作僵硬，但要先建立可重定向的人形骨架，驗證握刀、腳底與衣服穿插。
2. CC0 環境材質配合自製建築，維持統一的卡通色階；加入 LOD、貼圖預算與共用材質。
3. 比較現有 Toon 與 MToon 的同一角色同一光源渲染，再決定整合；不僅為增加套件而替換。
4. BLEACH 角色以自製模型／逐項明確授權的同人模型製作；公開立繪或模型不能自動標成我們原創。
5. 音樂用逐曲確認的開放原創曲或自行作曲；角色配音自行錄製或明確授權。此次沒有找到可確認開放再散布的官方 OST 或聲優語音包。

真正匯入時，逐檔記錄作者、來源、授權版本、修改、SHA-256、骨架與技術測試；本次沒有新增素材檔，也沒有改動遊戲行為。
