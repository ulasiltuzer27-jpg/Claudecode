--!strict
-- Net.luau
-- ========
-- Remote TANIMLARININ TEK NOKTASI.
--
-- Depo konvansiyonu: dis dunyaya acilan her yuzeyin tek bir tanim yeri
-- olur (C# tarafinda TransportFactory.cs ile ayni fikir). Burada da
-- remote adlari, yonleri ve argüman semalari tek tabloda; sunucu ve
-- istemci ayni tablodan okuyor. Bir remote'u yalnizca bir tarafta
-- tanimlamak ya da adini bir tarafta yanlis yazmak mumkun degil -
-- tools/verify_luau.py iki tarafi da bu tabloya karsi denetliyor.
--
-- RemoteFunction KULLANILMIYOR
-- ----------------------------
-- RemoteFunction sunucuyu istemcinin cevabini beklerken askida birakir;
-- kotu niyetli bir istemci cevabi hic dondurmeyerek sunucu thread'ini
-- kilitleyebilir. Her sey tek yonlu event; sunucu durumu StateUpdate ile
-- kendisi yayinliyor.

local RunService = game:GetService("RunService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local Net = {}

-- Argüman semalari Validate.luau'daki tip adlariyla yaziliyor.
-- Bos liste = argümansiz.
Net.Schema = {
	-- ── Sunucu -> Istemci ──────────────────────────────────────────────
	StateUpdate = { direction = "toClient", args = { "table" } },
	ChartUpdate = { direction = "toClient", args = { "table" } },
	Notify = { direction = "toClient", args = { "string", "string", "table" } },
	ToolResult = { direction = "toClient", args = { "string", "table" } },
	SurgeryBegin = { direction = "toClient", args = { "table" } },
	SurgeryEnd = { direction = "toClient", args = { "boolean", "number" } },
	ProgressUpdate = { direction = "toClient", args = { "table" } },
	LobbyState = { direction = "toClient", args = { "table" } },

	-- ── Istemci -> Sunucu ──────────────────────────────────────────────
	CallNext = { direction = "toServer", args = {} },
	SelectTool = { direction = "toServer", args = { "string" } },
	UseTool = { direction = "toServer", args = { "string" } },
	SubmitDiagnosis = { direction = "toServer", args = { "string" } },
	ApplyTreatment = { direction = "toServer", args = { "string" } },
	SurgeryInput = { direction = "toServer", args = { "number" } },
	Discharge = { direction = "toServer", args = {} },
	BuyTool = { direction = "toServer", args = { "string" } },
	BuyUpgrade = { direction = "toServer", args = { "string" } },
	SaveSettings = { direction = "toServer", args = { "table" } },
	RequestProgress = { direction = "toServer", args = {} },
	JoinQueue = { direction = "toServer", args = { "string" } },
	LeaveQueue = { direction = "toServer", args = {} },
}

-- Istemcinin saniyede kac kez gonderebilecegi. Dogrulama sunucuda
-- (Validate.luau); buradaki tablo yalnizca hiz limitini tanimliyor.
Net.RateLimit = {
	CallNext = 4,
	SelectTool = 12,
	UseTool = 6,
	SubmitDiagnosis = 4,
	ApplyTreatment = 6,
	SurgeryInput = 20,
	Discharge = 4,
	BuyTool = 4,
	BuyUpgrade = 4,
	-- Ayar kaydetme seyrek olmali: her kaydirici hareketinde DataStore'a
	-- yazmak hem gereksiz hem de kota yakar. Istemci de kendi tarafinda
	-- bekletiyor (SettingsPanel).
	SaveSettings = 3,
	RequestProgress = 3,
	JoinQueue = 4,
	LeaveQueue = 4,
}

local folderName = "VetRemotes"
local cache: { [string]: RemoteEvent } = {}

--- SUNUCU: butun remote'lari olusturur. init.server.luau bir kez cagirir.
function Net.createAll()
	assert(RunService:IsServer(), "Net.createAll yalnizca sunucuda cagrilabilir")

	local folder = ReplicatedStorage:FindFirstChild(folderName)
	if folder == nil then
		folder = Instance.new("Folder")
		folder.Name = folderName
		folder.Parent = ReplicatedStorage
	end

	for name in Net.Schema do
		if folder:FindFirstChild(name) == nil then
			local remote = Instance.new("RemoteEvent")
			remote.Name = name
			remote.Parent = folder
		end
	end
end

--- Adiyla remote getirir. Istemcide ilk cagride bekler.
function Net.get(name: string): RemoteEvent
	local cached = cache[name]
	if cached ~= nil then
		return cached
	end

	assert(Net.Schema[name] ~= nil, "Net: tanimsiz remote: " .. name)

	local folder = ReplicatedStorage:WaitForChild(folderName)
	local remote = folder:WaitForChild(name)
	assert(remote:IsA("RemoteEvent"), "Net: " .. name .. " RemoteEvent degil")

	cache[name] = remote
	return remote
end

--- SUNUCU: tek oyuncuya yollar.
function Net.toPlayer(name: string, player: Player, ...: any)
	assert(Net.Schema[name].direction == "toClient", "Net: " .. name .. " istemciye yollanamaz")
	Net.get(name):FireClient(player, ...)
end

--- ISTEMCI: sunucuya yollar.
function Net.toServer(name: string, ...: any)
	assert(Net.Schema[name].direction == "toServer", "Net: " .. name .. " sunucuya yollanamaz")
	Net.get(name):FireServer(...)
end

return Net
