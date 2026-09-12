--!strict
-- Leaderboard.luau
-- ================
-- Roblox'un yerlesik oyuncu listesi (Tab tusu) icin `leaderstats`.
--
-- Neden ayri modul?
-- -----------------
-- leaderstats, Roblox'un ISIM ve YAPI olarak dayattigi bir sozlesme:
-- Player altinda "leaderstats" adinda bir Folder, icinde IntValue'lar.
-- Bu sozlesmeyi PatientFlow'un icine dagitmak, oyun mantiginin arayuz
-- detayina bulasmasi olurdu. Tek dokunma noktasi burasi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Loc = require(Shared.Loc)

local Leaderboard = {}

--- Oyuncuya leaderstats klasorunu takar. Zaten varsa dokunmaz.
function Leaderboard.attach(player: Player)
	if player:FindFirstChild("leaderstats") ~= nil then
		return
	end

	local folder = Instance.new("Folder")
	folder.Name = "leaderstats"

	local money = Instance.new("IntValue")
	money.Name = Loc.get("board.money")
	money.Value = 0
	money.Parent = folder

	local level = Instance.new("IntValue")
	level.Name = Loc.get("board.level")
	level.Value = 1
	level.Parent = folder

	local patients = Instance.new("IntValue")
	patients.Name = Loc.get("board.patients")
	patients.Value = 0
	patients.Parent = folder

	folder.Parent = player
end

--- Degerleri gunceller. Oyuncu ayrilmissa sessizce cikar.
function Leaderboard.update(player: Player, data: any)
	local folder = player:FindFirstChild("leaderstats")
	if folder == nil then
		return
	end

	local money = folder:FindFirstChild(Loc.get("board.money"))
	if money ~= nil and money:IsA("IntValue") then
		money.Value = math.floor(data.money)
	end

	local level = folder:FindFirstChild(Loc.get("board.level"))
	if level ~= nil and level:IsA("IntValue") then
		level.Value = data.level
	end

	local patients = folder:FindFirstChild(Loc.get("board.patients"))
	if patients ~= nil and patients:IsA("IntValue") then
		patients.Value = data.stats.patients
	end
end

return Leaderboard
