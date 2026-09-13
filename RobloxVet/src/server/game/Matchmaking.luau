--!strict
-- Matchmaking.luau
-- ================
-- Lobideki sira ve ayrilmis sunucuya isinlanma. SUNUCU OTORITER.
--
-- Nasil calisiyor?
-- ----------------
-- Oyun TEK yer dosyasi. Herkese acik sunucu lobi, ayrilmis (reserved)
-- sunucu klinik olarak aciliyor; ayrim `game.PrivateServerId` ile.
-- Kadro dolunca:
--
--     TeleportService:ReserveServer(game.PlaceId)   -> erisim kodu
--     TeleportAsync(placeId, grup, options)         -> grup birlikte gider
--
-- Iki ayri place yayinlamaya ve PlaceId yapistirmaya gerek yok: oyun
-- yayinlandigi anda calisir.
--
-- Studio yedegi
-- -------------
-- Studio isinlanmayi desteklemiyor ve yayinlanmamis yerde PlaceId 0.
-- Cagrilar `pcall` icinde; basarisiz olursa `onFallback` cagriliyor ve
-- klinik AYNI sunucuda basliyor. Bu yedek istege bagli degil ZORUNLU:
-- olmasaydi dosya Studio'da acildiginda lobiden ileri gidilemezdi.

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local TeleportService = game:GetService("TeleportService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local plan = require(Data.lobby)

local Matchmaking = {}

export type Mode = { id: string, capacity: number, nameKey: string }

local modes: { [string]: Mode } = {}
local order: { string } = {}
for _, portal in plan.portals do
	modes[portal.id] = { id = portal.id, capacity = portal.capacity, nameKey = portal.nameKey }
	table.insert(order, portal.id)
end

-- mod -> sirada bekleyen oyuncular (giris sirasiyla)
local queues: { [string]: { Player } } = {}
for id in modes do
	queues[id] = {}
end

local waitingSince: { [Player]: number } = {}
local launching: { [string]: boolean } = {}

local onChanged: (() -> ())? = nil
local onFallback: ((group: { Player }, modeId: string) -> ())? = nil
local started = false

function Matchmaking.modes(): { Mode }
	local list = {}
	for _, id in order do
		table.insert(list, modes[id])
	end
	return list
end

function Matchmaking.isMode(id: string): boolean
	return modes[id] ~= nil
end

--- Oyuncu hangi sirada? Hicbirinde degilse nil.
function Matchmaking.modeOf(player: Player): string?
	for id, queue in queues do
		for _, waiting in queue do
			if waiting == player then
				return id
			end
		end
	end
	return nil
end

local function notifyChanged()
	if onChanged ~= nil then
		onChanged()
	end
end

--- Oyuncuyu her siradan cikarir. Ayrilirken ve sira degistirirken.
function Matchmaking.leave(player: Player): boolean
	local removed = false
	for _, queue in queues do
		for index = #queue, 1, -1 do
			if queue[index] == player then
				table.remove(queue, index)
				removed = true
			end
		end
	end
	waitingSince[player] = nil
	if removed then
		notifyChanged()
	end
	return removed
end

--- Oyuncuyu bir siraya alir. Zaten o siradaysa CIKARIR (ayni portala
--- ikinci kez basmak "vazgectim" demek).
function Matchmaking.join(player: Player, modeId: string): string
	if modes[modeId] == nil then
		return "lobby.unknownMode"
	end
	if Matchmaking.modeOf(player) == modeId then
		Matchmaking.leave(player)
		return "lobby.left"
	end

	Matchmaking.leave(player)
	table.insert(queues[modeId], player)
	waitingSince[player] = os.clock()
	notifyChanged()
	return "lobby.joined"
end

--- Bir grubu ayrilmis sunucuya yollar. Basarisiz olursa yedege duser.
local function launch(modeId: string, group: { Player })
	if #group == 0 or launching[modeId] then
		return
	end
	launching[modeId] = true

	for _, player in group do
		Matchmaking.leave(player)
	end
	notifyChanged()

	local ok, err = pcall(function()
		local code = TeleportService:ReserveServer(game.PlaceId)
		local options = Instance.new("TeleportOptions")
		options.ReservedServerAccessCode = code
		options:SetTeleportData({ mode = modeId, size = #group })
		TeleportService:TeleportAsync(game.PlaceId, group, options)
	end)

	if ok then
		launching[modeId] = false
		return
	end

	-- Studio ya da yayinlanmamis yer: klinigi burada baslat.
	warn(("[Eslestirme] isinlanma yapilamadi (%s) — klinik AYNI sunucuda aciliyor."):format(tostring(err)))
	launching[modeId] = false
	if onFallback ~= nil then
		onFallback(group, modeId)
	end
end

--- Istemciye yollanan goruntu.
function Matchmaking.snapshot(): any
	local list = {}
	for _, id in order do
		local queue = queues[id]
		local oldest = math.huge
		for _, player in queue do
			oldest = math.min(oldest, waitingSince[player] or os.clock())
		end
		local waited = if oldest == math.huge then 0 else os.clock() - oldest
		table.insert(list, {
			id = id,
			nameKey = modes[id].nameKey,
			capacity = modes[id].capacity,
			waiting = #queue,
			-- Yedek sayacin kalan suresi: arayuzde geri sayim.
			fallbackIn = math.max(0, plan.soloFallbackSeconds - waited),
		})
	end
	return { modes = list, players = #Players:GetPlayers() }
end

--- Sira dongusunu baslatir.
--- `changed` sira degisince, `fallback` isinlanma basarisiz olunca.
function Matchmaking.start(changed: () -> (), fallback: (group: { Player }, modeId: string) -> ())
	assert(RunService:IsServer(), "Matchmaking yalnizca sunucuda")
	if started then
		return
	end
	started = true
	onChanged = changed
	onFallback = fallback

	task.spawn(function()
		while true do
			task.wait(1)
			for _, id in order do
				local queue = queues[id]
				local mode = modes[id]
				if #queue == 0 then
					continue
				end

				-- Kadro doldu: ilk `capacity` oyuncu birlikte gider.
				if #queue >= mode.capacity then
					local group = {}
					for index = 1, mode.capacity do
						table.insert(group, queue[index])
					end
					launch(id, group)
					continue
				end

				-- Kadro dolmadi ama en uzun bekleyen yeterince bekledi:
				-- oyun yeniyken kimse sirada kilitli kalmasin.
				local oldest = math.huge
				for _, player in queue do
					oldest = math.min(oldest, waitingSince[player] or os.clock())
				end
				if oldest ~= math.huge and os.clock() - oldest >= plan.soloFallbackSeconds then
					launch(id, table.clone(queue))
				end
			end
			notifyChanged()
		end
	end)

	Players.PlayerRemoving:Connect(function(player: Player)
		Matchmaking.leave(player)
	end)
end

return Matchmaking
