--!strict
-- Lobby.luau
-- ==========
-- Lobi salonunu kurar: giris holü, dort portal kemeri ve ustlerinde
-- SIRADAKI OYUNCU SAYISI.
--
-- Klinigin esya kutuphanesi burada da kullaniliyor (Props.luau); lobi
-- icin ayri bir esya seti yazmak ayni isi iki kez yapmak olurdu.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)
local Detail = require(Shared.Detail)
local Loc = require(Shared.Loc)

local Props = require(script.Parent.Props)
local SkyLighting = require(script.Parent.Lighting)

local plan = require(Data.lobby)

local Lobby = {}

export type Portal = {
	id: string,
	capacity: number,
	cframe: CFrame,
	prompt: ProximityPrompt,
	counter: TextLabel,
	pad: BasePart,
}

export type Built = {
	model: Model,
	portals: { [string]: Portal },
	spawn: Vector3,
}

local function colorOf(name: string): Color3
	return Build.color(plan.colors[name])
end

--- Portal kemeri: iki sutun, ust kiris, icinde renkli bir perde ve
--- ustunde sayac tabelasi. Perde yari saydam: arkasindaki duvarin
--- gorunmesi "gecis" hissini veriyor.
local function buildPortal(parent: Instance, portal: any): Portal
	local model = Build.model("Portal_" .. portal.id, parent)
	local color = Build.color(portal.color)
	local origin = CFrame.new(portal.at[1], plan.wallHeight * 0, portal.at[2])
	local width, height = 9, 12

	for _, side in { -1, 1 } do
		Build.part({
			name = "Sutun",
			size = Vector3.new(1.1, height, 1.6),
			cf = origin * CFrame.new(side * (width * 0.5 + 0.55), height * 0.5, 0),
			color = colorOf("trim"),
			material = Enum.Material.Marble,
			parent = model,
		})
		Detail.bolt(model, origin * CFrame.new(side * (width * 0.5 + 0.55), height - 0.8, 0.85), 0.4, color)
	end

	Build.part({
		name = "Kiris",
		size = Vector3.new(width + 2.2, 1.6, 1.8),
		cf = origin * CFrame.new(0, height + 0.8, 0),
		color = colorOf("trim"),
		material = Enum.Material.Marble,
		parent = model,
	})

	-- Perde: yari saydam renkli yuzey. Neon DEGIL - lobi de goz yormasin.
	Build.part({
		name = "Perde",
		size = Vector3.new(width, height - 0.4, 0.3),
		cf = origin * CFrame.new(0, height * 0.5, 0),
		color = color,
		material = Enum.Material.Glass,
		transparency = 0.55,
		canCollide = false,
		castShadow = false,
		parent = model,
	})

	-- Zemin pedi: oyuncunun uzerine basacagi yer.
	local pad = Build.part({
		name = "Ped",
		size = Vector3.new(width, 0.3, 5),
		cf = origin * CFrame.new(0, 0.15, -3),
		color = color,
		material = Enum.Material.SmoothPlastic,
		parent = model,
	})
	Detail.trim(model, pad.CFrame, pad.Size, 0.2, colorOf("trim"))

	-- Tabela: mod adi + sirada bekleyen sayisi.
	-- Portallar salonun kuzeyinde; oyuncu guneyden geliyor. Plaka'nin
	-- On yuzu -Z'ye baksin diye 180 derece dondurulüyor.
	local plateCF = origin * CFrame.new(0, height + 3, 0) * CFrame.Angles(0, math.pi, 0)
	Detail.plate(model, plateCF, 8, 1.8, colorOf("trim"), color, Loc.get(portal.nameKey))

	local board = Build.part({
		name = "Sayac",
		size = Vector3.new(6, 1.6, 0.3),
		cf = origin * CFrame.new(0, height + 1.1, -0.9),
		color = colorOf("trim"),
		canCollide = false,
		castShadow = false,
		parent = model,
	})
	local counter = Build.surfaceText(board, Enum.NormalId.Back, "", Color3.fromRGB(246, 250, 252))

	local prompt = Instance.new("ProximityPrompt")
	prompt.Name = "Katil"
	prompt.ActionText = Loc.get("lobby.join")
	prompt.ObjectText = Loc.get(portal.nameKey)
	prompt.HoldDuration = 0
	prompt.MaxActivationDistance = 10
	prompt.RequiresLineOfSight = false
	prompt.Parent = pad

	return {
		id = portal.id,
		capacity = portal.capacity,
		cframe = origin,
		prompt = prompt,
		counter = counter,
		pad = pad,
	}
end

--- Lobiyi kurar.
function Lobby.build(parent: Instance): Built
	local root = Build.model("Lobi", parent)
	local min, max = plan.bounds.min, plan.bounds.max
	local half = plan.groundSize * 0.5

	Build.slab({ -half, -half }, { half, half }, -0.5, 1, colorOf("ground"), Enum.Material.Grass, root, "Zemin")
	Build.slab(min, max, -0.25, 0.5, colorOf("floor"), Enum.Material.Marble, root, "Doseme")
	Detail.tiles(root, min, max, 0.03, 6, colorOf("floor"), colorOf("floorTile"), Enum.Material.Marble)
	Build.slab(min, max, plan.wallHeight - 0.25, 0.5, colorOf("ceiling"), Enum.Material.SmoothPlastic, root, "Tavan")

	-- Orta hali: gozun merkeze gitmesi icin.
	Build.slab({ -14, -14 }, { 14, 14 }, 0.1, 0.2, colorOf("carpet"), Enum.Material.Fabric, root, "Hali")

	-- Duvarlar: dort kenar, portallarin oldugu kuzey duvari da dahil
	-- (portal kemerleri duvarin onunde duruyor).
	local th = plan.wallThickness
	local sides = {
		{ Vector3.new(max[1] - min[1] + th * 2, plan.wallHeight, th), Vector3.new((min[1] + max[1]) / 2, plan.wallHeight / 2, min[2]) },
		{ Vector3.new(max[1] - min[1] + th * 2, plan.wallHeight, th), Vector3.new((min[1] + max[1]) / 2, plan.wallHeight / 2, max[2]) },
		{ Vector3.new(th, plan.wallHeight, max[2] - min[2]), Vector3.new(min[1], plan.wallHeight / 2, (min[2] + max[2]) / 2) },
		{ Vector3.new(th, plan.wallHeight, max[2] - min[2]), Vector3.new(max[1], plan.wallHeight / 2, (min[2] + max[2]) / 2) },
	}
	for _, side in sides do
		Build.part({
			name = "Duvar",
			size = side[1],
			cf = CFrame.new(side[2]),
			color = colorOf("wall"),
			material = Enum.Material.SmoothPlastic,
			parent = root,
		})
	end
	-- Supurgelik ve kornis
	for _, side in sides do
		Build.part({
			name = "Supurgelik",
			size = Vector3.new(side[1].X + 0.2, 0.9, side[1].Z + 0.2),
			cf = CFrame.new(side[2].X, 0.45, side[2].Z),
			color = colorOf("trim"),
			canCollide = false,
			castShadow = false,
			parent = root,
		})
		Build.part({
			name = "Kornis",
			size = Vector3.new(side[1].X + 0.3, 0.8, side[1].Z + 0.3),
			cf = CFrame.new(side[2].X, plan.wallHeight - 0.9, side[2].Z),
			color = colorOf("trim"),
			canCollide = false,
			castShadow = false,
			parent = root,
		})
	end

	-- Tavan lambalari
	local lamp = SkyLighting.lamp("ceiling")
	for _, position in { { -20, -12 }, { 20, -12 }, { -20, 8 }, { 20, 8 }, { 0, -2 } } do
		Build.ceilingLight(Vector3.new(position[1], plan.wallHeight - 0.9, position[2]), root, lamp)
	end

	-- Buyuk karsilama tabelasi
	local welcome = Build.part({
		name = "Tabela",
		size = Vector3.new(30, 4.5, 0.6),
		cf = CFrame.new(0, 14, min[2] + 0.9),
		color = colorOf("trim"),
		material = Enum.Material.Marble,
		canCollide = false,
		parent = root,
	})
	-- Karsilama tabelasi salonun GUNEY duvarinda; oyuncu iceriden,
	-- yani +Z tarafindan bakiyor: On yuz.
	Build.surfaceText(welcome, Enum.NormalId.Front, Loc.get("lobby.title"), Color3.fromRGB(246, 250, 252))
	local hint = Build.part({
		name = "Aciklama",
		size = Vector3.new(30, 2, 0.4),
		cf = CFrame.new(0, 10.6, min[2] + 0.9),
		color = colorOf("wall"),
		canCollide = false,
		castShadow = false,
		parent = root,
	})
	Build.surfaceText(hint, Enum.NormalId.Front, Loc.get("lobby.hint"), colorOf("trim"))

	local furniture = Build.model("Esyalar", root)
	for _, spec in plan.props do
		Props.build(
			spec.id,
			CFrame.new(spec.at[1], 0, spec.at[2]) * CFrame.Angles(0, math.rad(spec.rot or 0), 0),
			furniture
		)
	end

	local portals: { [string]: Portal } = {}
	for _, spec in plan.portals do
		portals[spec.id] = buildPortal(root, spec)
	end

	local spawnPoint = Instance.new("SpawnLocation")
	spawnPoint.Name = "LobiSpawn"
	spawnPoint.Size = Vector3.new(14, 1, 10)
	spawnPoint.CFrame = CFrame.new(plan.spawn.at[1], 0.5, plan.spawn.at[2])
	spawnPoint.Anchored = true
	spawnPoint.Color = colorOf("carpet")
	spawnPoint.Material = Enum.Material.Fabric
	spawnPoint.TopSurface = Enum.SurfaceType.Smooth
	spawnPoint.Neutral = true
	spawnPoint.Duration = 0
	spawnPoint.Parent = root

	return {
		model = root,
		portals = portals,
		spawn = Vector3.new(plan.spawn.at[1], 0, plan.spawn.at[2]),
	}
end

return Lobby
