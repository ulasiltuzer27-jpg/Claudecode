--!strict
-- Lobby.luau
-- ==========
-- Lobi salonunu kurar. Salon UC BOLUM:
--
--     guney  z -40..-18  GIRIS HOLU     karsilama bankosu, koltuklar,
--                                       duyuru panolari, saksilar
--            z = -18     ── kapili ic duvar ──
--     orta   z -18..14   ANA SALON      cesme, sutun dizileri, heykel,
--                                       hali yollari, pankartlar
--            z = 14      ── kapili ic duvar ──
--     kuzey  z 14..40    PORTAL KANADI  dort kemer, dort portal
--
-- Neden bu kadar?
-- ---------------
-- Lobi oyunun ILK gordugu oda ve en bos odasiydi: dort duz duvar,
-- birkac bank, dort portal. Oyuncunun oyun hakkindaki ilk izlenimi
-- oradan cikiyor. Salon artik bir YOL sunuyor - kapidan gir, bankoyu
-- gec, cesmenin etrafindan dolan, kemerin altindan portala yurü.
--
-- Duvarlar, kapilar ve pencereler VERIDE (`data/lobby.json` -> walls)
-- ve klinikle AYNI kurucudan geciyor (`src/shared/Walls.luau`). Once
-- kodda uretilen dort duz duvardi; veriye tasininca `verify_layout`
-- onlari da olcmeye basladi - kapi onune konan bir esya artik denetimde
-- yakalaniyor.
--
-- Klinigin esya kutuphanesi burada da kullaniliyor (Props.luau); lobi
-- icin ayri bir esya seti yazmak ayni isi iki kez yapmak olurdu.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)
local Detail = require(Shared.Detail)
local Loc = require(Shared.Loc)
local Walls = require(Shared.Walls)

local Doors = require(script.Parent.Doors)
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
	doors: Model,
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
	Detail.tiles(root, min, max, 0.03, 8, colorOf("floor"), colorOf("floorTile"), Enum.Material.Marble)

	-- Girise giden tas yol: bina disindaki tek zemin parcasi.
	local path = plan.outdoor.path
	local pathHalf = path.width * 0.5
	Build.slab(
		{ path.from[1] - pathHalf, math.min(path.from[2], path.to[2]) },
		{ path.from[1] + pathHalf, math.max(path.from[2], path.to[2]) },
		-0.2, 0.45, colorOf("path"), Enum.Material.Concrete, root, "Yol"
	)

	-- Tavan: ORTA acikligi bos birakiliyor (aydinlik). Duz bir tavan
	-- 96x80'lik bir salonu kutuya ceviriyordu.
	local lightMin, lightMax = { -18, -10 }, { 18, 10 }
	Build.slab(min, { max[1], lightMin[2] }, plan.wallHeight - 0.25, 0.5, colorOf("ceiling"), Enum.Material.SmoothPlastic, root, "Tavan")
	Build.slab({ min[1], lightMax[2] }, max, plan.wallHeight - 0.25, 0.5, colorOf("ceiling"), Enum.Material.SmoothPlastic, root, "Tavan")
	Build.slab({ min[1], lightMin[2] }, { lightMin[1], lightMax[2] }, plan.wallHeight - 0.25, 0.5, colorOf("ceiling"), Enum.Material.SmoothPlastic, root, "Tavan")
	Build.slab({ lightMax[1], lightMin[2] }, { max[1], lightMax[2] }, plan.wallHeight - 0.25, 0.5, colorOf("ceiling"), Enum.Material.SmoothPlastic, root, "Tavan")

	-- Aydinlik camı ve kafesi: gunes salonun ortasina dusuyor.
	local sky = Build.slab(
		lightMin, lightMax, plan.wallHeight - 0.2, 0.3,
		colorOf("glass"), Enum.Material.Glass, root, "Aydinlik"
	)
	sky.Transparency = 0.55
	sky.CanCollide = false
	sky.CastShadow = false
	for index = -3, 3 do
		Build.part({
			name = "AydinlikKafesi",
			size = Vector3.new(0.4, 0.4, lightMax[2] - lightMin[2]),
			cf = CFrame.new(index * 5, plan.wallHeight - 0.05, 0),
			color = colorOf("trim"),
			canCollide = false,
			castShadow = false,
			parent = root,
		})
	end
	for index = -2, 2 do
		Build.part({
			name = "AydinlikKafesi",
			size = Vector3.new(lightMax[1] - lightMin[1], 0.4, 0.4),
			cf = CFrame.new(0, plan.wallHeight - 0.05, index * 4),
			color = colorOf("trim"),
			canCollide = false,
			castShadow = false,
			parent = root,
		})
	end

	-- Duvarlar VERIDEN ve klinikle AYNI kurucudan.
	for _, wall in plan.walls do
		Walls.build(plan :: any, root, wall, colorOf("wall"), colorOf("trim"))
	end

	-- Tavan lambalari: aydinligin disinda, dort kose. Cok lamba sahneyi
	-- yikiyor; aydinlik zaten ortayi aydinlatiyor.
	local lamp = SkyLighting.lamp("ceiling")
	for _, position in { { -26, -30 }, { 26, -30 }, { -30, 0 }, { 30, 0 }, { -20, 28 }, { 20, 28 } } do
		Build.ceilingLight(Vector3.new(position[1], plan.wallHeight - 0.9, position[2]), root, lamp)
	end

	-- Buyuk karsilama tabelasi
	local welcome = Build.part({
		name = "Tabela",
		size = Vector3.new(30, 4.5, 0.6),
		cf = CFrame.new(0, 15.5, min[2] + 0.9),
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
		cf = CFrame.new(0, 12.4, min[2] + 0.9),
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
	-- Bina disi: yolun iki yani ve bahce.
	for _, spec in plan.outdoor.props do
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

	-- Kapilar: klinikteki AYNI modul, lobi planiyla. Lobide de kapiyi
	-- kendin aciyorsun.
	local doors = Doors.build(root, plan :: any)

	return {
		model = root,
		portals = portals,
		spawn = Vector3.new(plan.spawn.at[1], 0, plan.spawn.at[2]),
		doors = doors,
	}
end

return Lobby
