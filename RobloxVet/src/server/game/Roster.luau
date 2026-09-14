--!strict
-- Roster.luau
-- ===========
-- Klinik sunucusunun KADROSU: bu sunucuya kimlerin girmeye hakki var?
--
-- Neden var?
-- ----------
-- "Solo girdim, sunucu bana ozel olsun; baska biri solo girince benim
-- yanima dusmesin." Eslestirme her kadro icin AYRI bir ayrilmis sunucu
-- rezerve ediyor, yani normal akista bu zaten boyle. Ama kod bunu
-- yalnizca UMUYORDU; hicbir yerde ZORLAMIYORDU:
--
--   - ayrilmis sunucuya erisim kodu bilen bir istemci girebilir,
--   - arkadas listesinden "katil" bazi ayarlarda ayrilmis sunucuya
--     dusurebilir,
--   - Studio yedeginde (isinlanma yok) klinik LOBI sunucusunda
--     aciliyordu ve sonraki her grup ayni klinige ekleniyordu - solo
--     giren iki oyuncu birbirinin yanina dusuyordu. Kullanicinin
--     sikayeti tam olarak buydu.
--
-- Kadro burada TUTULUYOR ve giren her oyuncu ona karsi denetleniyor.
-- Listede olmayan oyuncu oyuna alinmiyor; lobiye geri yollaniyor.
--
-- Kadro nereden geliyor?
-- ----------------------
-- Isinlanma verisinden (`TeleportData.roster`): eslestirme grubu
-- yollarken UserId listesini de koyuyor. Ayrilmis sunucuda ILK giren
-- oyuncunun katilma verisi kadroyu kuruyor; sonrakiler ona karsi
-- olculuyor.
--
-- Isinlanma verisi yoksa (Studio yedegi ya da dogrudan giris) kadro
-- `Roster.claim` ile YERINDE kuruluyor: sunucuyu ilk acan grup sahibi
-- oluyor ve sunucu baskasina kapaniyor.

local Players = game:GetService("Players")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local plan = require(Data.lobby)

local Roster = {}

local allowed: { [number]: boolean } = {}
local count = 0
local modeId: string? = nil
local claimed = false

--- Kadro kuruldu mu?
function Roster.claimed(): boolean
	return claimed
end

function Roster.size(): number
	return count
end

function Roster.modeId(): string?
	return modeId
end

--- Kadroyu kurar. Ikinci cagri YOK SAYILIYOR: sunucu ilk gruba ait ve
--- sonradan gelen bir grup onu genisletemez. Sessizce genisletmek,
--- tam da kacinmak istedigimiz "ikinci solo oyuncu yanima dustu"
--- durumunu geri getirirdi.
function Roster.claim(userIds: { number }, mode: string?): boolean
	if claimed then
		return false
	end
	claimed = true
	modeId = mode
	for _, id in userIds do
		if allowed[id] ~= true then
			allowed[id] = true
			count += 1
		end
	end
	return true
end

--- Oyuncu grubunu kadroya cevirir (Player -> UserId).
function Roster.claimPlayers(group: { Player }, mode: string?): boolean
	local ids = {}
	for _, player in group do
		table.insert(ids, player.UserId)
	end
	return Roster.claim(ids, mode)
end

--- Bu oyuncunun kadroda yeri var mi?
--- Kadro hic kurulmamissa HERKES kabul ediliyor: kadrosuz bir sunucu
--- (ornegin dogrudan klinige giris) kimseyi disari atmamali.
function Roster.allows(player: Player): boolean
	if not claimed then
		return true
	end
	return allowed[player.UserId] == true
end

--- Oyuncunun katilma verisindeki kadroyu okur. Ayrilmis sunucuda ILK
--- giren oyuncu icin cagriliyor.
---
--- Katilma verisi ISTEMCIDEN GELMIYOR: `Player:GetJoinData()` sunucunun
--- Roblox'tan okudugu veri ve isinlanmayi baslatan sunucu yazdi. Yine de
--- bicimi denetleniyor - bozuk bir tablo yuzunden kadro bos kalirsa
--- sunucu herkese acilirdi.
function Roster.adopt(player: Player): boolean
	if claimed then
		return false
	end
	local ok, join = pcall(function()
		return player:GetJoinData()
	end)
	if not ok or type(join) ~= "table" then
		return false
	end
	local data = (join :: any).TeleportData
	if type(data) ~= "table" then
		return false
	end
	local ids = data.roster
	if type(ids) ~= "table" then
		return false
	end
	local clean = {}
	for _, id in ids do
		if type(id) == "number" and id > 0 then
			table.insert(clean, id)
		end
	end
	if #clean == 0 then
		return false
	end
	local mode = if type(data.mode) == "string" then data.mode else nil
	return Roster.claim(clean, mode)
end

--- Kadroda olmayan oyuncuyu lobiye (herkese acik yere) geri yollar.
--- `TeleportService` Studio'da calismiyor; orada oyuncu KICK ediliyor
--- ki sunucu yine de ozel kalsin. Sessizce iceride birakmak, ozelligin
--- hic olmamasiyla ayni sey olurdu.
function Roster.sendBack(player: Player)
	local TeleportService = game:GetService("TeleportService")
	local ok = pcall(function()
		TeleportService:Teleport(game.PlaceId, player)
	end)
	if ok then
		return
	end
	player:Kick(plan.privacy.kickMessage)
end

--- Kadro disi oyuncular icin bekci. `Roster.start()` klinik
--- sunucusunda bir kez cagriliyor.
function Roster.start()
	Players.PlayerAdded:Connect(function(player: Player)
		if not claimed then
			Roster.adopt(player)
		end
		if Roster.allows(player) then
			return
		end
		task.delay(plan.privacy.returnDelaySeconds, function()
			if player.Parent ~= nil and not Roster.allows(player) then
				Roster.sendBack(player)
			end
		end)
	end)
end

return Roster
