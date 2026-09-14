--!strict
-- Props.luau
-- ==========
-- Klinik esyalarinin kod ile uretimi. Her esya temel parcalardan
-- (Part / WedgePart / silindir / kure) kuruluyor; hicbir dis asset'e
-- bagimli degil, bu yuzden oyun ilk acilista eksiksiz gorunuyor.
--
-- Detay nereden geliyor?
-- ----------------------
-- Tekrar eden detaylar (kenar pahi, kulp, konik ayak, kablo, kenarlik,
-- cerceveli ekran) `Shared/Detail.luau` icinde tek yerde. Esyalar onu
-- cagiriyor; boylece 26 esya ayni kalite dilinden konusuyor ve kod
-- 26 kez tekrar etmiyor.
--
-- Parlama kurali
-- --------------
-- `Material.Neon` yalnizca GERCEKTEN isik yayan yuzeylerde: tavan
-- lambasi difuzoru (Build.ceilingLight), ameliyat lambasi merceği ve
-- tabeladaki hac. Monitor ve rontgen EKRANLARI Neon degil - gercek bir
-- ekran karanlik odada etrafini aydinlatmaz ve ilk surumde her sey
-- parliyordu.
--
-- Isik parlakliklari data/lighting.json'dan geliyor.
--
-- Renkler neden burada, olculer neden veride?
-- -------------------------------------------
-- Oynanisi ve yerlesimi etkileyen her sey veride (data/clinic.json,
-- data/props.json). Saf dekorasyon rengi oynanisi degistirmiyor; onu da
-- veriye tasimak kimsenin duzenlemeyecegi 150 satirlik bir tabloya mal
-- olurdu. Sinir burada cizildi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Assets = require(Shared.Assets)
local Build = require(Shared.Build)
local Detail = require(Shared.Detail)
local Loc = require(Shared.Loc)

local SkyLighting = require(script.Parent.Lighting)

local Props = {}

local C = {
	steel = Color3.fromRGB(188, 194, 200),
	darkSteel = Color3.fromRGB(122, 129, 138),
	chrome = Color3.fromRGB(214, 219, 224),
	white = Color3.fromRGB(242, 245, 248),
	offWhite = Color3.fromRGB(224, 229, 234),
	shadowWhite = Color3.fromRGB(198, 205, 212),
	wood = Color3.fromRGB(172, 128, 84),
	midWood = Color3.fromRGB(146, 106, 68),
	darkWood = Color3.fromRGB(112, 80, 52),
	teal = Color3.fromRGB(70, 160, 162),
	deepTeal = Color3.fromRGB(42, 114, 120),
	paleTeal = Color3.fromRGB(168, 212, 212),
	navy = Color3.fromRGB(46, 60, 82),
	screen = Color3.fromRGB(198, 224, 234),
	pad = Color3.fromRGB(92, 142, 180),
	darkPad = Color3.fromRGB(66, 108, 142),
	leaf = Color3.fromRGB(84, 148, 76),
	darkLeaf = Color3.fromRGB(58, 112, 54),
	midLeaf = Color3.fromRGB(102, 164, 88),
	bark = Color3.fromRGB(104, 78, 54),
	darkBark = Color3.fromRGB(78, 58, 40),
	soil = Color3.fromRGB(92, 70, 54),
	terracotta = Color3.fromRGB(178, 104, 76),
	glass = Color3.fromRGB(206, 228, 238),
	accent = Color3.fromRGB(226, 104, 94),
	water = Color3.fromRGB(118, 184, 220),
	towel = Color3.fromRGB(238, 228, 210),
	black = Color3.fromRGB(40, 44, 50),
	rubber = Color3.fromRGB(58, 62, 68),
	-- Dekorasyon renkleri (data/decor.json'daki sus esyalari icin)
	sand = Color3.fromRGB(226, 208, 168),
	gold = Color3.fromRGB(214, 176, 92),
	blossom = Color3.fromRGB(232, 148, 176),
}

type Builder = (model: Model, origin: CFrame) -> ()

--- Her esyanin icindeki tekrar eden kalibi kisaltir.
local function maker(model: Model, origin: CFrame)
	return function(
		name: string,
		size: Vector3,
		offset: Vector3,
		color: Color3,
		material: Enum.Material?,
		rotation: CFrame?
	): Part
		local cf = origin * CFrame.new(offset)
		if rotation ~= nil then
			cf = cf * rotation
		end
		return Build.part({
			name = name,
			size = size,
			cf = cf,
			color = color,
			material = material or Enum.Material.SmoothPlastic,
			parent = model,
		})
	end
end

--- Detay parcasi: carpismaya ve golgeye katilmaz.
local function makerDecor(model: Model, origin: CFrame)
	return function(
		name: string,
		size: Vector3,
		offset: Vector3,
		color: Color3,
		material: Enum.Material?,
		rotation: CFrame?
	): Part
		local cf = origin * CFrame.new(offset)
		if rotation ~= nil then
			cf = cf * rotation
		end
		return Build.part({
			name = name,
			size = size,
			cf = cf,
			color = color,
			material = material or Enum.Material.SmoothPlastic,
			canCollide = false,
			castShadow = false,
			parent = model,
		})
	end
end

local builders: { [string]: Builder } = {}

-- ── Resepsiyon ve bekleme ─────────────────────────────────────────────

builders.receptionDesk = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Govde", Vector3.new(10, 3.2, 2.4), Vector3.new(0, 1.6, 0), C.wood, Enum.Material.Wood)
	d("GovdeSerit", Vector3.new(10.2, 0.3, 2.5), Vector3.new(0, 0.25, 0), C.darkWood)
	-- On yuz: uc panel, arada dikey ayirici
	for index = -1, 1 do
		d("OnPanel", Vector3.new(2.9, 2.2, 0.12), Vector3.new(index * 3.1, 1.8, -1.26), C.midWood)
	end
	d("Tezgah", Vector3.new(11, 0.4, 3.4), Vector3.new(0, 3.4, 0.3), C.offWhite)
	Detail.trim(model, origin * CFrame.new(0, 3.4, 0.3), Vector3.new(11, 0.4, 3.4), 0.14, C.shadowWhite)
	d("TezgahBandi", Vector3.new(11.2, 0.5, 0.24), Vector3.new(0, 2.95, -1.42), C.teal)

	Detail.screen(model, origin * CFrame.new(-3, 4.5, 0.5) * CFrame.Angles(math.rad(-14), 0, 0),
		2.8, 1.8, C.black, C.screen, Loc.get("prop.monitor"))
	d("MonitorAyagi", Vector3.new(0.5, 1.1, 0.5), Vector3.new(-3, 3.9, 0.5), C.black)
	d("MonitorTabani", Vector3.new(1.5, 0.14, 1), Vector3.new(-3, 3.65, 0.5), C.black)
	Detail.cable(model, (origin * CFrame.new(-3, 3.6, 0.5)).Position, (origin * CFrame.new(-4.6, 3.6, -0.6)).Position, 0.09, C.black)

	d("Klavye", Vector3.new(2.4, 0.12, 0.9), Vector3.new(-3, 3.66, 1.5), C.offWhite)
	d("Kagitlar", Vector3.new(1.6, 0.12, 1.1), Vector3.new(2.4, 3.66, 0.4), C.white)
	d("Dosya", Vector3.new(1.2, 0.3, 0.9), Vector3.new(4, 3.75, 0.2), C.accent)
	Build.pillar({
		name = "KalemKutusu", size = Vector3.new(0.7, 0.9, 0.7),
		cf = origin * CFrame.new(1.1, 4.05, 0.6), color = C.deepTeal,
		canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Zil", size = Vector3.new(0.6, 0.3, 0.6),
		cf = origin * CFrame.new(3.3, 3.75, 1.2), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
end

builders.waitingBench = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Oturak", Vector3.new(7, 0.45, 2.2), Vector3.new(0, 1.7, 0), C.teal)
	-- Uc ayri minder: tek bir uzun kutu yerine bolunmus oturak
	for index = -1, 1 do
		d("Minder", Vector3.new(2.1, 0.3, 2), Vector3.new(index * 2.25, 2.02, 0), C.paleTeal, Enum.Material.Fabric)
	end
	p("Sirtlik", Vector3.new(7, 2, 0.35), Vector3.new(0, 2.85, -1), C.deepTeal)
	for index = -1, 1 do
		d("SirtMinderi", Vector3.new(2.1, 1.5, 0.22), Vector3.new(index * 2.25, 2.9, -0.8), C.paleTeal, Enum.Material.Fabric)
	end
	d("Kolcak", Vector3.new(0.3, 0.9, 2), Vector3.new(-3.5, 2.4, 0), C.darkSteel, Enum.Material.Metal)
	d("Kolcak", Vector3.new(0.3, 0.9, 2), Vector3.new(3.5, 2.4, 0), C.darkSteel, Enum.Material.Metal)
	Detail.leg(model, origin * CFrame.new(-3, 1.7, 0.8), 1.7, 0.28, C.darkSteel)
	Detail.leg(model, origin * CFrame.new(3, 1.7, 0.8), 1.7, 0.28, C.darkSteel)
	Detail.leg(model, origin * CFrame.new(-3, 1.7, -0.8), 1.7, 0.28, C.darkSteel)
	Detail.leg(model, origin * CFrame.new(3, 1.7, -0.8), 1.7, 0.28, C.darkSteel)
end

builders.outdoorBench = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Oturak", Vector3.new(6, 0.3, 2), Vector3.new(0, 1.6, 0), C.wood, Enum.Material.WoodPlanks)
	for index = 0, 3 do
		d("Lata", Vector3.new(5.8, 0.16, 0.4), Vector3.new(0, 1.82, -0.75 + index * 0.5), C.midWood, Enum.Material.WoodPlanks)
	end
	p("Sirtlik", Vector3.new(6, 1.6, 0.3), Vector3.new(0, 2.55, -0.9), C.wood, Enum.Material.WoodPlanks)
	for index = 0, 2 do
		d("SirtLatasi", Vector3.new(5.8, 0.4, 0.16), Vector3.new(0, 2.05 + index * 0.5, -0.78), C.midWood, Enum.Material.WoodPlanks)
	end
	d("YanKasa", Vector3.new(0.35, 1.6, 1.9), Vector3.new(-2.6, 0.8, 0), C.darkWood, Enum.Material.Wood)
	d("YanKasa", Vector3.new(0.35, 1.6, 1.9), Vector3.new(2.6, 0.8, 0), C.darkWood, Enum.Material.Wood)
	d("Kolcak", Vector3.new(0.35, 0.22, 2.1), Vector3.new(-2.6, 2.5, -0.1), C.darkWood, Enum.Material.Wood)
	d("Kolcak", Vector3.new(0.35, 0.22, 2.1), Vector3.new(2.6, 2.5, -0.1), C.darkWood, Enum.Material.Wood)
end

builders.waterCooler = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Govde", Vector3.new(1.7, 3.4, 1.7), Vector3.new(0, 1.7, 0), C.white)
	d("GovdeOluk", Vector3.new(1.8, 0.16, 1.8), Vector3.new(0, 2.5, 0), C.shadowWhite)
	d("Panel", Vector3.new(1.2, 1.1, 0.12), Vector3.new(0, 2.2, 0.86), C.navy)
	d("Musluk", Vector3.new(0.34, 0.5, 0.5), Vector3.new(-0.3, 2.75, 0.92), C.teal)
	d("Musluk", Vector3.new(0.34, 0.5, 0.5), Vector3.new(0.3, 2.75, 0.92), C.accent)
	d("Damlalik", Vector3.new(1.2, 0.12, 0.6), Vector3.new(0, 1.85, 0.9), C.steel, Enum.Material.Metal)
	Build.pillar({
		name = "Damacana", size = Vector3.new(1.5, 2.2, 1.5),
		cf = origin * CFrame.new(0, 4.55, 0), color = C.water,
		material = Enum.Material.Glass, transparency = 0.35,
		canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "DamacanaBoynu", size = Vector3.new(0.6, 0.6, 0.6),
		cf = origin * CFrame.new(0, 3.4, 0), color = C.paleTeal,
		transparency = 0.25, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Bardaklik", size = Vector3.new(0.55, 1.4, 0.55),
		cf = origin * CFrame.new(1.1, 2.7, 0), color = C.offWhite,
		canCollide = false, castShadow = false, parent = model,
	})
end

builders.trashBin = function(model, origin)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Kova", size = Vector3.new(1.55, 2.1, 1.55),
		cf = origin * CFrame.new(0, 1.05, 0), color = C.steel,
		material = Enum.Material.Metal, parent = model,
	})
	Build.pillar({
		name = "AltHalka", size = Vector3.new(1.65, 0.14, 1.65),
		cf = origin * CFrame.new(0, 0.16, 0), color = C.darkSteel,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "UstHalka", size = Vector3.new(1.7, 0.16, 1.7),
		cf = origin * CFrame.new(0, 2.02, 0), color = C.darkSteel,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Kapak", size = Vector3.new(1.72, 0.22, 1.72),
		cf = origin * CFrame.new(0, 2.2, 0), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "KapakDelik", size = Vector3.new(0.95, 0.1, 0.95),
		cf = origin * CFrame.new(0, 2.3, 0), color = C.black,
		canCollide = false, castShadow = false, parent = model,
	})
	d("Pedal", Vector3.new(0.7, 0.14, 0.35), Vector3.new(0, 0.2, 0.95), C.darkSteel, Enum.Material.Metal)
	d("PedalKolu", Vector3.new(0.12, 1.9, 0.12), Vector3.new(0.55, 1.1, 0.7), C.darkSteel, Enum.Material.Metal)
	d("Etiket", Vector3.new(0.8, 0.8, 0.06), Vector3.new(0, 1.2, 0.8), C.teal)
	d("Menteşe", Vector3.new(0.5, 0.16, 0.2), Vector3.new(0, 2.2, -0.75), C.darkSteel, Enum.Material.Metal)
	-- Icindeki torba: kapagin deliginden gorunuyor.
	Build.pillar({
		name = "Torba", size = Vector3.new(1.4, 0.3, 1.4),
		cf = origin * CFrame.new(0, 1.95, 0), color = C.rubber,
		canCollide = false, castShadow = false, parent = model,
	})
end

builders.poster = function(model, origin)
	local d = makerDecor(model, origin)
	d("Cerceve", Vector3.new(5.4, 3.8, 0.14), Vector3.new(0, 7, 0), C.deepTeal)
	local board = d("Afis", Vector3.new(5, 3.4, 0.18), Vector3.new(0, 7, 0.06), C.white)
	d("UstKenar", Vector3.new(5.4, 0.18, 0.2), Vector3.new(0, 8.9, 0.02), C.teal)
	d("AltKenar", Vector3.new(5.4, 0.18, 0.2), Vector3.new(0, 5.1, 0.02), C.teal)
	d("Ask", Vector3.new(0.12, 0.5, 0.12), Vector3.new(-1.6, 9.1, 0), C.darkSteel, Enum.Material.Metal)
	d("Ask", Vector3.new(0.12, 0.5, 0.12), Vector3.new(1.6, 9.1, 0), C.darkSteel, Enum.Material.Metal)
	d("Ray", Vector3.new(3.6, 0.12, 0.12), Vector3.new(0, 9.3, 0), C.darkSteel, Enum.Material.Metal)
	for _, corner in { Vector3.new(-2.5, 8.7, 0.1), Vector3.new(2.5, 8.7, 0.1), Vector3.new(-2.5, 5.3, 0.1), Vector3.new(2.5, 5.3, 0.1) } do
		Detail.bolt(model, origin * CFrame.new(corner), 0.22, C.chrome)
	end
	Build.surfaceText(board, Enum.NormalId.Front, Loc.get("prop.poster"), C.navy)
end

-- ── Eczane ve depolama ────────────────────────────────────────────────

builders.medicineShelf = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Kasa", Vector3.new(6.5, 8, 1.7), Vector3.new(0, 4, 0), C.offWhite)
	d("ArkaPano", Vector3.new(6.1, 7.6, 0.1), Vector3.new(0, 4, -0.75), C.paleTeal)
	d("YanDuvar", Vector3.new(0.2, 7.8, 1.7), Vector3.new(-3.15, 4, 0), C.shadowWhite)
	d("YanDuvar", Vector3.new(0.2, 7.8, 1.7), Vector3.new(3.15, 4, 0), C.shadowWhite)
	d("UstKornis", Vector3.new(6.8, 0.3, 1.9), Vector3.new(0, 8, 0), C.teal)

	local bottleColors = { C.accent, C.teal, C.water, C.leaf, C.navy, C.towel }
	for shelf = 1, 4 do
		local y = 1.2 + (shelf - 1) * 1.9
		d("Raf", Vector3.new(6.2, 0.2, 1.55), Vector3.new(0, y, 0.05), C.white)
		d("RafKenari", Vector3.new(6.2, 0.12, 0.12), Vector3.new(0, y + 0.14, 0.82), C.darkSteel, Enum.Material.Metal)
		for slot = 1, 6 do
			local x = -2.6 + (slot - 1) * 1.05
			local color = bottleColors[((shelf + slot) % #bottleColors) + 1]
			Build.pillar({
				name = "Sise", size = Vector3.new(0.55, 1.05, 0.55),
				cf = origin * CFrame.new(x, y + 0.63, 0.1), color = color,
				material = Enum.Material.Glass, transparency = 0.18,
				canCollide = false, castShadow = false, parent = model,
			})
			Build.pillar({
				name = "SiseKapagi", size = Vector3.new(0.32, 0.2, 0.32),
				cf = origin * CFrame.new(x, y + 1.25, 0.1), color = C.white,
				canCollide = false, castShadow = false, parent = model,
			})
		end
	end
end

builders.cabinet = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Govde", Vector3.new(4.5, 6, 1.9), Vector3.new(0, 3, 0), C.white)
	d("Taban", Vector3.new(4.6, 0.3, 2), Vector3.new(0, 0.15, 0), C.shadowWhite)
	Detail.panel(model, origin * CFrame.new(-1.1, 4.1, 0.98), 2, 3.4, C.offWhite, C.shadowWhite, C.darkSteel)
	Detail.panel(model, origin * CFrame.new(1.1, 4.1, 0.98), 2, 3.4, C.offWhite, C.shadowWhite, C.darkSteel)
	Detail.drawer(model, origin * CFrame.new(0, 1.9, 0.98), 4.1, 0.95, C.offWhite, C.darkSteel)
	Detail.drawer(model, origin * CFrame.new(0, 0.85, 0.98), 4.1, 0.95, C.offWhite, C.darkSteel)
	d("UstTabla", Vector3.new(4.8, 0.28, 2.1), Vector3.new(0, 6.05, 0), C.shadowWhite)
	Detail.trim(model, origin * CFrame.new(0, 6.05, 0), Vector3.new(4.8, 0.28, 2.1), 0.14, C.teal)
	Detail.plate(model, origin * CFrame.new(0, 5.5, 0.98), 1.8, 0.5, C.deepTeal, C.white, Loc.get("prop.cabinet"))
end

builders.towelShelf = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Kasa", Vector3.new(4, 5, 1.5), Vector3.new(0, 2.5, 0), C.offWhite)
	d("ArkaPano", Vector3.new(3.7, 4.7, 0.1), Vector3.new(0, 2.5, -0.68), C.paleTeal)
	d("YanDuvar", Vector3.new(0.16, 4.8, 1.5), Vector3.new(-1.92, 2.5, 0), C.shadowWhite)
	d("YanDuvar", Vector3.new(0.16, 4.8, 1.5), Vector3.new(1.92, 2.5, 0), C.shadowWhite)
	d("UstKornis", Vector3.new(4.3, 0.24, 1.7), Vector3.new(0, 5, 0), C.teal)
	local colors = { C.towel, C.glass, C.paleTeal }
	for shelf = 1, 3 do
		local y = 1 + (shelf - 1) * 1.6
		d("Raf", Vector3.new(3.8, 0.16, 1.35), Vector3.new(0, y, 0), C.white)
		for side = -1, 1, 2 do
			local color = colors[((shelf + side + 2) % #colors) + 1]
			d("Havlu", Vector3.new(1.5, 0.36, 1.1), Vector3.new(side * 0.9, y + 0.28, 0), color, Enum.Material.Fabric)
			d("Havlu", Vector3.new(1.5, 0.36, 1.1), Vector3.new(side * 0.9, y + 0.66, 0), color, Enum.Material.Fabric)
		end
	end
end

builders.dryerRack = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Dikme", Vector3.new(0.3, 5, 0.3), Vector3.new(-2.5, 2.5, 0), C.darkSteel, Enum.Material.Metal)
	p("Dikme", Vector3.new(0.3, 5, 0.3), Vector3.new(2.5, 2.5, 0), C.darkSteel, Enum.Material.Metal)
	d("AyakKizak", Vector3.new(0.4, 0.18, 2.2), Vector3.new(-2.5, 0.1, 0), C.darkSteel, Enum.Material.Metal)
	d("AyakKizak", Vector3.new(0.4, 0.18, 2.2), Vector3.new(2.5, 0.1, 0), C.darkSteel, Enum.Material.Metal)
	for index = 0, 2 do
		d("Ray", Vector3.new(5.3, 0.18, 0.18), Vector3.new(0, 3.2 + index * 0.8, 0), C.chrome, Enum.Material.Metal)
	end
	d("AsiliHavlu", Vector3.new(1.6, 2.2, 0.14), Vector3.new(-1.2, 3.6, 0), C.towel, Enum.Material.Fabric)
	d("AsiliHavlu", Vector3.new(1.5, 2.6, 0.14), Vector3.new(0.4, 3.4, 0), C.water, Enum.Material.Fabric)
	d("AsiliHavlu", Vector3.new(1.3, 1.9, 0.14), Vector3.new(1.8, 3.8, 0), C.paleTeal, Enum.Material.Fabric)
	Detail.bolt(model, origin * CFrame.new(-2.5, 4.8, 0.2), 0.3, C.chrome)
	Detail.bolt(model, origin * CFrame.new(2.5, 4.8, 0.2), 0.3, C.chrome)
end

-- ── Muayene ───────────────────────────────────────────────────────────

local function tableBase(model: Model, origin: CFrame, width: number, depth: number, topY: number)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Kolon", size = Vector3.new(1.5, topY - 0.4, 1.5),
		cf = origin * CFrame.new(0, (topY - 0.4) * 0.5 + 0.35, 0),
		color = C.chrome, material = Enum.Material.Metal, parent = model,
	})
	d("TabanPlakasi", Vector3.new(width * 0.45, 0.35, depth * 0.6), Vector3.new(0, 0.18, 0), C.darkSteel, Enum.Material.Metal)
	d("TabanPahi", Vector3.new(width * 0.5, 0.14, depth * 0.65), Vector3.new(0, 0.38, 0), C.steel, Enum.Material.Metal)
	d("YukseklikKolu", Vector3.new(0.16, 0.16, 1.1), Vector3.new(0.8, 1.1, 0), C.black, Enum.Material.Metal)
	Detail.bolt(model, origin * CFrame.new(0, 0.4, depth * 0.3), 0.35, C.darkSteel)
end

builders.examTable = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	tableBase(model, origin, 7.5, 4.2, 2.85)
	p("Tabla", Vector3.new(7.5, 0.45, 4.2), Vector3.new(0, 2.85, 0), C.steel, Enum.Material.Metal)
	Detail.trim(model, origin * CFrame.new(0, 2.85, 0), Vector3.new(7.5, 0.45, 4.2), 0.16, C.chrome)
	d("Minder", Vector3.new(7.1, 0.45, 3.9), Vector3.new(0, 3.3, 0), C.pad, Enum.Material.Fabric)
	-- Minder dikisleri
	for index = -1, 1 do
		d("Dikis", Vector3.new(0.1, 0.12, 3.9), Vector3.new(index * 2.2, 3.53, 0), C.darkPad, Enum.Material.Fabric)
	end
	Detail.rim(model, origin * CFrame.new(0, 3.62, 0), Vector3.new(7.5, 0.2, 4.2), 0.22, C.chrome)

	-- Yan tepsi
	d("TepsiAyak", Vector3.new(0.22, 3, 0.22), Vector3.new(4.9, 1.5, 0), C.darkSteel, Enum.Material.Metal)
	d("TepsiTabani", Vector3.new(1, 0.14, 1), Vector3.new(4.9, 0.1, 0), C.darkSteel, Enum.Material.Metal)
	local tray = d("Tepsi", Vector3.new(2.4, 0.16, 1.6), Vector3.new(4.9, 3, 0), C.chrome, Enum.Material.Metal)
	Detail.rim(model, origin * CFrame.new(4.9, 3.1, 0), Vector3.new(2.4, 0.12, 1.6), 0.14, C.steel)
	for index = 1, 3 do
		d("Alet", Vector3.new(0.14, 0.1, 1.1), Vector3.new(4.3 + (index - 1) * 0.55, 3.14, 0), C.chrome, Enum.Material.Metal)
	end
	local _ = tray
end

builders.surgeryTable = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	tableBase(model, origin, 8.5, 4.8, 3.05)
	p("Tabla", Vector3.new(8.5, 0.5, 4.8), Vector3.new(0, 3.05, 0), C.steel, Enum.Material.Metal)
	Detail.trim(model, origin * CFrame.new(0, 3.05, 0), Vector3.new(8.5, 0.5, 4.8), 0.18, C.chrome)
	d("Ortu", Vector3.new(8.2, 0.3, 4.5), Vector3.new(0, 3.45, 0), C.deepTeal, Enum.Material.Fabric)
	d("OrtuKenari", Vector3.new(8.3, 0.5, 0.16), Vector3.new(0, 3.2, 2.3), C.deepTeal, Enum.Material.Fabric)
	d("OrtuKenari", Vector3.new(8.3, 0.5, 0.16), Vector3.new(0, 3.2, -2.3), C.deepTeal, Enum.Material.Fabric)
	-- Yan raylar: dikmeler uzerinde
	for _, side in { -1, 1 } do
		d("Ray", Vector3.new(8.2, 0.16, 0.16), Vector3.new(0, 3.9, side * 2.45), C.chrome, Enum.Material.Metal)
		for index = -1, 1 do
			d("RayDikmesi", Vector3.new(0.14, 0.65, 0.14), Vector3.new(index * 3, 3.6, side * 2.45), C.chrome, Enum.Material.Metal)
		end
	end
	-- Alet tepsisi
	d("TepsiAyak", Vector3.new(0.26, 3.4, 0.26), Vector3.new(5.6, 1.7, 0), C.darkSteel, Enum.Material.Metal)
	d("Tepsi", Vector3.new(3, 0.18, 1.8), Vector3.new(5.6, 3.4, 0), C.chrome, Enum.Material.Metal)
	Detail.rim(model, origin * CFrame.new(5.6, 3.5, 0), Vector3.new(3, 0.12, 1.8), 0.14, C.steel)
	for index = 1, 5 do
		d("Alet", Vector3.new(0.13, 0.09, 1.2), Vector3.new(4.5 + (index - 1) * 0.55, 3.54, 0), C.chrome, Enum.Material.Metal)
	end
	d("AtikKabi", Vector3.new(0.9, 0.9, 0.9), Vector3.new(5.6, 3.95, 0.6), C.accent)
end

builders.sink = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Dolap", Vector3.new(4.3, 2.8, 2.2), Vector3.new(0, 1.4, 0), C.white)
	Detail.panel(model, origin * CFrame.new(-1, 1.5, 1.14), 1.8, 2.1, C.offWhite, C.shadowWhite, C.darkSteel)
	Detail.panel(model, origin * CFrame.new(1, 1.5, 1.14), 1.8, 2.1, C.offWhite, C.shadowWhite, C.darkSteel)
	p("Tezgah", Vector3.new(4.7, 0.35, 2.5), Vector3.new(0, 3, 0), C.shadowWhite)
	Detail.trim(model, origin * CFrame.new(0, 3, 0), Vector3.new(4.7, 0.35, 2.5), 0.14, C.steel)
	d("Sifonluk", Vector3.new(2.6, 0.5, 1.8), Vector3.new(0, 2.9, 0.1), C.chrome, Enum.Material.Metal)
	d("Lavabo", Vector3.new(2.2, 0.4, 1.4), Vector3.new(0, 2.98, 0.1), C.steel, Enum.Material.Metal)
	d("Gider", Vector3.new(0.35, 0.08, 0.35), Vector3.new(0, 2.8, 0.1), C.darkSteel, Enum.Material.Metal)
	Build.pillar({
		name = "Musluk", size = Vector3.new(0.2, 1.5, 0.2),
		cf = origin * CFrame.new(0, 3.9, -0.8), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	d("MuslukUcu", Vector3.new(0.18, 0.18, 1.1), Vector3.new(0, 4.6, -0.35), C.chrome, Enum.Material.Metal)
	d("Kol", Vector3.new(0.5, 0.14, 0.14), Vector3.new(-0.5, 4.55, -0.8), C.chrome, Enum.Material.Metal)
	d("Kol", Vector3.new(0.5, 0.14, 0.14), Vector3.new(0.5, 4.55, -0.8), C.chrome, Enum.Material.Metal)
	d("SabunKabi", Vector3.new(0.45, 0.9, 0.45), Vector3.new(1.7, 3.65, 0), C.teal)
	d("SabunPompasi", Vector3.new(0.16, 0.3, 0.16), Vector3.new(1.7, 4.2, 0), C.white)
	d("AynaCercevesi", Vector3.new(3, 2.4, 0.12), Vector3.new(0, 6, -1.15), C.chrome, Enum.Material.Metal)
	d("Ayna", Vector3.new(2.7, 2.1, 0.06), Vector3.new(0, 6, -1.08), C.glass, Enum.Material.Glass)
end

builders.monitor = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Taban", Vector3.new(1.7, 0.3, 1.3), Vector3.new(0, 0.15, 0), C.black)
	Build.pillar({
		name = "Direk", size = Vector3.new(0.28, 4, 0.28),
		cf = origin * CFrame.new(0, 2.2, 0), color = C.darkSteel,
		material = Enum.Material.Metal, parent = model,
	})
	d("Mafsal", Vector3.new(0.5, 0.5, 0.5), Vector3.new(0, 4.2, 0), C.black)
	Detail.screen(model, origin * CFrame.new(0, 4.7, 0.2) * CFrame.Angles(math.rad(-10), 0, 0),
		4.4, 2.8, C.black, C.screen, Loc.get("prop.monitor"))
	d("Dugme", Vector3.new(0.2, 0.2, 0.1), Vector3.new(1.6, 3.3, 0.3), C.teal)
	d("Dugme", Vector3.new(0.2, 0.2, 0.1), Vector3.new(1.9, 3.3, 0.3), C.accent)
	Detail.cable(model, (origin * CFrame.new(0, 0.3, -0.4)).Position, (origin * CFrame.new(1.4, 0.2, -1.6)).Position, 0.1, C.black)
end

builders.ivStand = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Taban", Vector3.new(1.9, 0.22, 1.9), Vector3.new(0, 0.11, 0), C.darkSteel, Enum.Material.Metal)
	-- Bes tekerlek: gercek serum askisi gibi
	for index = 1, 5 do
		local angle = (index / 5) * math.pi * 2
		local x, z = math.cos(angle) * 0.95, math.sin(angle) * 0.95
		d("AyakKolu", Vector3.new(0.9, 0.14, 0.14), Vector3.new(x * 0.5, 0.18, z * 0.5), C.darkSteel,
			Enum.Material.Metal, CFrame.Angles(0, -angle, 0))
		Build.pillar({
			name = "Teker", size = Vector3.new(0.3, 0.16, 0.3),
			cf = origin * CFrame.new(x, 0.15, z) * CFrame.Angles(0, 0, math.rad(90)),
			color = C.rubber, canCollide = false, castShadow = false, parent = model,
		})
	end
	Build.pillar({
		name = "Direk", size = Vector3.new(0.24, 7, 0.24),
		cf = origin * CFrame.new(0, 3.5, 0), color = C.chrome,
		material = Enum.Material.Metal, parent = model,
	})
	d("Kelepce", Vector3.new(0.4, 0.3, 0.4), Vector3.new(0, 4.4, 0), C.darkSteel, Enum.Material.Metal)
	d("Kanca", Vector3.new(1.2, 0.14, 0.14), Vector3.new(0.45, 6.9, 0), C.chrome, Enum.Material.Metal)
	d("KancaUcu", Vector3.new(0.14, 0.4, 0.14), Vector3.new(1, 6.72, 0), C.chrome, Enum.Material.Metal)
	d("Serum", Vector3.new(0.95, 1.8, 0.4), Vector3.new(0.95, 5.9, 0), C.water, Enum.Material.Glass)
	d("SerumEtiketi", Vector3.new(0.7, 0.5, 0.06), Vector3.new(0.95, 5.9, 0.23), C.white)
	Detail.cable(model, (origin * CFrame.new(0.95, 5, 0)).Position, (origin * CFrame.new(0.3, 2.6, 0.7)).Position, 0.08, C.paleTeal)
end

builders.scale = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Platform", Vector3.new(4, 0.45, 3), Vector3.new(0, 0.32, 0), C.steel, Enum.Material.Metal)
	Detail.trim(model, origin * CFrame.new(0, 0.32, 0), Vector3.new(4, 0.45, 3), 0.14, C.chrome)
	d("Kaymaz", Vector3.new(3.6, 0.1, 2.6), Vector3.new(0, 0.6, 0), C.rubber, Enum.Material.Fabric)
	for index = -1, 1 do
		d("KaymazOluk", Vector3.new(3.4, 0.06, 0.12), Vector3.new(0, 0.66, index * 0.8), C.black, Enum.Material.Fabric)
	end
	Build.pillar({
		name = "Direk", size = Vector3.new(0.22, 3, 0.22),
		cf = origin * CFrame.new(0, 1.85, -1.3), color = C.darkSteel,
		material = Enum.Material.Metal, parent = model,
	})
	Detail.screen(model, origin * CFrame.new(0, 3.5, -1.22) * CFrame.Angles(math.rad(-16), 0, 0),
		1.9, 1.05, C.black, C.screen, "0.0 kg")
	d("Dugme", Vector3.new(0.3, 0.14, 0.14), Vector3.new(-0.6, 3, -1.2), C.teal)
	d("Dugme", Vector3.new(0.3, 0.14, 0.14), Vector3.new(0.6, 3, -1.2), C.accent)
end

builders.xrayMachine = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Taban", Vector3.new(4.2, 0.6, 3.4), Vector3.new(0, 0.3, 0), C.darkSteel, Enum.Material.Metal)
	Detail.trim(model, origin * CFrame.new(0, 0.3, 0), Vector3.new(4.2, 0.6, 3.4), 0.16, C.steel)
	p("Kasa", Vector3.new(3.4, 5.4, 2.6), Vector3.new(0, 3.2, 0), C.offWhite)
	d("KasaSerit", Vector3.new(3.5, 0.24, 2.7), Vector3.new(0, 5, 0), C.teal)
	Detail.panel(model, origin * CFrame.new(0, 2.6, 1.34), 2.6, 2.4, C.white, C.shadowWhite, C.darkSteel)
	-- Kol ve kafa
	Build.pillar({
		name = "Direk", size = Vector3.new(0.5, 3.2, 0.5),
		cf = origin * CFrame.new(0, 7.2, 0), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	d("Kol", Vector3.new(0.55, 0.55, 3.6), Vector3.new(0, 8.5, 1.6), C.chrome, Enum.Material.Metal)
	d("Mafsal", Vector3.new(0.8, 0.8, 0.8), Vector3.new(0, 8.5, 0), C.darkSteel, Enum.Material.Metal)
	d("Kafa", Vector3.new(2.4, 1.5, 2.4), Vector3.new(0, 7.9, 3.1), C.white)
	Detail.trim(model, origin * CFrame.new(0, 7.9, 3.1), Vector3.new(2.4, 1.5, 2.4), 0.14, C.shadowWhite)

	-- Mercek: ISIK KAYNAGI, o yuzden hafif isiyor. Parlaklik veriden.
	local lamp = SkyLighting.lamp("xrayLens")
	local lens = d("Mercek", Vector3.new(1.6, 0.18, 1.6), Vector3.new(0, 7.12, 3.1), lamp.color, Enum.Material.Neon)
	local glow = Instance.new("PointLight")
	glow.Color = lamp.color
	glow.Brightness = lamp.brightness
	glow.Range = lamp.range
	glow.Parent = lens

	Detail.screen(model, origin * CFrame.new(2.1, 4.4, -0.9) * CFrame.Angles(0, math.rad(-35), 0),
		2.4, 1.7, C.black, C.screen, Loc.get("prop.xray"))
	d("PanelKolu", Vector3.new(0.16, 1.6, 0.16), Vector3.new(2.1, 3.2, -0.9), C.darkSteel, Enum.Material.Metal)
	Detail.cable(model, (origin * CFrame.new(0, 0.6, -1.3)).Position, (origin * CFrame.new(-1.6, 0.2, -2.4)).Position, 0.12, C.black)
end

builders.surgeryLight = function(model, origin)
	local d = makerDecor(model, origin)
	local lamp = SkyLighting.lamp("surgeryLamp")
	d("Baglanti", Vector3.new(0.4, 3.2, 0.4), Vector3.new(0, 11.4, 0), C.chrome, Enum.Material.Metal)
	d("TavanPlakasi", Vector3.new(1.6, 0.2, 1.6), Vector3.new(0, 12.9, 0), C.steel, Enum.Material.Metal)
	d("Mafsal", Vector3.new(0.7, 0.7, 0.7), Vector3.new(0, 9.9, 0), C.darkSteel, Enum.Material.Metal)
	Build.pillar({
		name = "Kubbe", size = Vector3.new(5, 1, 5),
		cf = origin * CFrame.new(0, 9.55, 0), color = C.steel,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "KubbeKenari", size = Vector3.new(5.3, 0.22, 5.3),
		cf = origin * CFrame.new(0, 9.1, 0), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	-- Mercek: gercekten isik yayiyor, Neon burada hakli.
	local lens = Build.pillar({
		name = "Mercek", size = Vector3.new(4.4, 0.3, 4.4),
		cf = origin * CFrame.new(0, 8.95, 0), color = lamp.color,
		material = Enum.Material.Neon, canCollide = false, castShadow = false, parent = model,
	})
	-- Ic mercekler: tek bir daire yerine cok gozlu ameliyat lambasi
	for index = 1, 6 do
		local angle = (index / 6) * math.pi * 2
		Build.pillar({
			name = "IcMercek", size = Vector3.new(1.1, 0.12, 1.1),
			cf = origin * CFrame.new(math.cos(angle) * 1.4, 8.88, math.sin(angle) * 1.4),
			color = C.chrome, material = Enum.Material.Metal,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	local spot = Instance.new("SpotLight")
	spot.Face = Enum.NormalId.Bottom
	spot.Angle = lamp.angle or 75
	spot.Brightness = lamp.brightness
	spot.Range = lamp.range
	spot.Color = lamp.color
	spot.Shadows = true
	spot.Parent = lens
end

-- ── Kogus ve yikama ───────────────────────────────────────────────────

builders.kennelPen = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- KOGUS PADOGU
	--
	-- Eskiden burada bes kucuk kafes vardi ve icine hicbir hayvan
	-- sigmiyordu: en buyuk hastanin ayak izi 6,4 stud, kafesin ici
	-- 4,8'di. Yatan hasta oynanisi gelince padok gercekten hayvan alacak
	-- olcuye buyudu (ic olcu 10,4 x 7,4) ve sayi 5'ten 3'e indi.
	-- `data/props.json` -> `shelter` bu ic olcuyu bildiriyor; yerlesim
	-- denetimi yatak noktasinin oraya sigdigini olcuyor.
	p("Taban", Vector3.new(10.8, 0.4, 7.8), Vector3.new(0, 0.2, 0), C.offWhite)
	d("TabanPahi", Vector3.new(11, 0.16, 8), Vector3.new(0, 0.42, 0), C.shadowWhite)
	d("Yatak", Vector3.new(7.4, 0.45, 5.2), Vector3.new(0, 0.64, -0.4), C.pad, Enum.Material.Fabric)
	d("YatakKenari", Vector3.new(7.6, 0.32, 0.35), Vector3.new(0, 0.8, -3), C.darkPad, Enum.Material.Fabric)
	d("Battaniye", Vector3.new(5.2, 0.12, 3.4), Vector3.new(0.6, 0.88, -0.2), C.towel, Enum.Material.Fabric)

	p("ArkaDuvar", Vector3.new(10.8, 5.6, 0.24), Vector3.new(0, 3, -3.9), C.white)
	p("YanDuvar", Vector3.new(0.24, 5.6, 7.8), Vector3.new(-5.3, 3, 0), C.white)
	p("YanDuvar", Vector3.new(0.24, 5.6, 7.8), Vector3.new(5.3, 3, 0), C.white)
	p("Tavan", Vector3.new(10.8, 0.3, 7.8), Vector3.new(0, 5.85, 0), C.offWhite)

	-- On yuz: cerceve + dikey parmakliklar + yatay kusaklar. Parmaklik
	-- araligi hayvani gormeye yetecek kadar genis.
	d("KapiCercevesi", Vector3.new(10.8, 0.26, 0.26), Vector3.new(0, 5.65, 3.9), C.darkSteel, Enum.Material.Metal)
	d("KapiCercevesi", Vector3.new(10.8, 0.26, 0.26), Vector3.new(0, 0.45, 3.9), C.darkSteel, Enum.Material.Metal)
	for index = 0, 12 do
		d("Parmaklik", Vector3.new(0.16, 5.1, 0.16), Vector3.new(-4.9 + index * 0.82, 3, 3.9), C.chrome, Enum.Material.Metal)
	end
	for index = 1, 2 do
		d("Kusak", Vector3.new(10.2, 0.16, 0.16), Vector3.new(0, 1.6 + (index - 1) * 2.6, 3.9), C.chrome, Enum.Material.Metal)
	end
	d("Mandal", Vector3.new(0.6, 0.5, 0.32), Vector3.new(4.2, 3, 4.08), C.darkSteel, Enum.Material.Metal)
	d("MandalKolu", Vector3.new(0.16, 1, 0.16), Vector3.new(4.2, 3.5, 4.14), C.chrome, Enum.Material.Metal)

	Detail.plate(model, origin * CFrame.new(-3.4, 5.95, 3.8), 2.6, 0.7, C.deepTeal, C.white, Loc.get("prop.kennel"))
	Build.pillar({
		name = "SuKabi", size = Vector3.new(1.3, 0.45, 1.3),
		cf = origin * CFrame.new(3.4, 0.62, 2.2), color = C.water,
		canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "MamaKabi", size = Vector3.new(1.3, 0.45, 1.3),
		cf = origin * CFrame.new(3.4, 0.62, 0.6), color = C.steel,
		material = Enum.Material.Metal,
		canCollide = false, castShadow = false, parent = model,
	})
	-- Serum askisi: yatan hastanin orada TEDAVI GORDUGUNU anlatan detay.
	d("SerumDiregi", Vector3.new(0.16, 4.4, 0.16), Vector3.new(-4.3, 2.4, 2.4), C.chrome, Enum.Material.Metal)
	d("SerumKolu", Vector3.new(0.9, 0.14, 0.14), Vector3.new(-3.9, 4.5, 2.4), C.chrome, Enum.Material.Metal)
	d("SerumTorbasi", Vector3.new(0.8, 1.2, 0.3), Vector3.new(-3.5, 3.9, 2.4), C.water, Enum.Material.Glass)
end

-- ── Laboratuvar ve karantina ──────────────────────────────────────────
-- Kuzey kanadi. Buradaki esyalarin hepsi gorunusten fazlasi: mikroskop
-- numune incelemenin YERI, karantina bolmesi de bulasici hastanin
-- kuyruktan cikarildigi yer. Ikisinin de konumu `data/clinic.json`
-- icinde (`spots.lab`, `podPoints`) ve yerlesim denetiminden geciyor.

builders.microscope = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Tezgah: mikroskop havada duramaz, altinda bir laboratuvar
	-- tezgahi var. Olcu butcesi (props.json) ikisini birlikte sayiyor.
	p("Tezgah", Vector3.new(4.4, 0.35, 3.0), Vector3.new(0, 2.6, 0), C.white)
	Detail.trim(model, origin * CFrame.new(0, 2.6, 0), Vector3.new(4.4, 0.35, 3.0), 0.12, C.steel)
	p("TezgahGovdesi", Vector3.new(4.2, 2.45, 2.8), Vector3.new(0, 1.22, 0), C.offWhite)
	Detail.drawer(model, origin * CFrame.new(0, 1.9, 1.42), 3.4, 0.8, C.shadowWhite, C.darkSteel)
	Detail.drawer(model, origin * CFrame.new(0, 0.95, 1.42), 3.4, 0.8, C.shadowWhite, C.darkSteel)

	-- Mikroskobun kendisi: ayak, kol, tup, objektif tablasi, ayna.
	d("Ayak", Vector3.new(1.5, 0.3, 1.1), Vector3.new(-0.7, 2.92, -0.3), C.darkSteel, Enum.Material.Metal)
	d("Kol", Vector3.new(0.34, 1.9, 0.34), Vector3.new(-1.15, 3.9, -0.45), C.darkSteel,
		Enum.Material.Metal, CFrame.Angles(math.rad(-12), 0, 0))
	d("Tabla", Vector3.new(1.3, 0.16, 1.0), Vector3.new(-0.6, 3.62, -0.2), C.black, Enum.Material.Metal)
	d("Lam", Vector3.new(0.7, 0.05, 0.45), Vector3.new(-0.6, 3.72, -0.16), C.glass, Enum.Material.Glass)
	d("Tup", Vector3.new(0.42, 1.15, 0.42), Vector3.new(-0.62, 4.55, -0.32), C.chrome,
		Enum.Material.Metal, CFrame.Angles(math.rad(14), 0, 0))
	d("Objektif", Vector3.new(0.3, 0.5, 0.3), Vector3.new(-0.6, 3.95, -0.2), C.steel, Enum.Material.Metal)
	d("Goz", Vector3.new(0.34, 0.5, 0.34), Vector3.new(-0.68, 5.18, -0.5), C.black,
		Enum.Material.Metal, CFrame.Angles(math.rad(14), 0, 0))
	d("Ayarvidasi", Vector3.new(0.4, 0.4, 0.22), Vector3.new(-1.42, 3.5, -0.45), C.chrome, Enum.Material.Metal)

	-- Tezgahin ustunde numune siseleri: laboratuvarin "calisiyor"
	-- gorunmesini saglayan sey renkli sivilardir.
	for index, tint in { C.accent, C.water, C.leaf } do
		Build.pillar({
			name = "Numune", size = Vector3.new(0.45, 0.9, 0.45),
			cf = origin * CFrame.new(0.6 + (index - 1) * 0.62, 3.25, 0.8),
			color = tint, material = Enum.Material.Glass,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	Detail.plate(model, origin * CFrame.new(0, 3.1, -1.52), 2.2, 0.6, C.deepTeal, C.white, Loc.get("prop.microscope"))
end

builders.centrifuge = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Tezgah", Vector3.new(3.8, 0.32, 3.8), Vector3.new(0, 2.4, 0), C.white)
	p("TezgahGovdesi", Vector3.new(3.6, 2.3, 3.6), Vector3.new(0, 1.15, 0), C.offWhite)
	Detail.trim(model, origin * CFrame.new(0, 2.4, 0), Vector3.new(3.8, 0.32, 3.8), 0.12, C.steel)

	-- Santrifujun govdesi silindir; kapagi ayri bir parca ki aralik
	-- gorunsun ve makine "acilabilir" dursun.
	Build.pillar({
		name = "Govde", size = Vector3.new(2.6, 1.5, 2.6),
		cf = origin * CFrame.new(0, 3.3, 0), color = C.shadowWhite, parent = model,
	})
	Build.pillar({
		name = "Kapak", size = Vector3.new(2.7, 0.35, 2.7),
		cf = origin * CFrame.new(0, 4.16, 0), color = C.darkSteel,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Pencere", size = Vector3.new(1.5, 0.12, 1.5),
		cf = origin * CFrame.new(0, 4.35, 0), color = C.glass,
		material = Enum.Material.Glass, canCollide = false, castShadow = false, parent = model,
	})
	-- Rotor gozleri: kapagin altindan gorunen tup yuvalari.
	for index = 1, 6 do
		local angle = (index / 6) * math.pi * 2
		d("TupYuvasi", Vector3.new(0.34, 0.7, 0.34), Vector3.new(math.cos(angle) * 0.8, 3.85, math.sin(angle) * 0.8),
			C.accent, Enum.Material.Glass, CFrame.Angles(math.rad(18), -angle, 0))
	end
	d("Panel", Vector3.new(1.1, 0.6, 0.12), Vector3.new(0, 3.1, 1.32), C.black)
	d("Dugme", Vector3.new(0.22, 0.22, 0.1), Vector3.new(-0.32, 3.1, 1.4), C.teal)
	d("Dugme", Vector3.new(0.22, 0.22, 0.1), Vector3.new(0.32, 3.1, 1.4), C.accent)
	Detail.cable(model, (origin * CFrame.new(0, 2.5, -1.7)).Position, (origin * CFrame.new(1.4, 0.2, -1.9)).Position, 0.1, C.black)
end

builders.sampleRack = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Govde", Vector3.new(5.0, 4.2, 1.9), Vector3.new(0, 2.1, 0), C.offWhite)
	Detail.trim(model, origin * CFrame.new(0, 2.1, 0), Vector3.new(5.0, 4.2, 1.9), 0.14, C.steel)
	-- Uc raf, her rafta bir sira numune tupu. Tuplerin rengi degisiyor:
	-- tek renk bir sira, uzaktan tek bir blok gibi gorunuyordu.
	local tints = { C.accent, C.water, C.leaf, C.gold, C.paleTeal, C.blossom }
	for shelf = 0, 2 do
		local y = 0.9 + shelf * 1.35
		d("Raf", Vector3.new(4.7, 0.14, 1.6), Vector3.new(0, y, 0.1), C.shadowWhite)
		for index = 1, 6 do
			Build.pillar({
				name = "Tup", size = Vector3.new(0.34, 0.95, 0.34),
				cf = origin * CFrame.new(-1.9 + (index - 1) * 0.76, y + 0.55, 0.1),
				color = tints[((shelf * 6 + index - 1) % #tints) + 1],
				material = Enum.Material.Glass,
				canCollide = false, castShadow = false, parent = model,
			})
		end
	end
	d("TasimaKolu", Vector3.new(4.2, 0.16, 0.16), Vector3.new(0, 4.34, 0.1), C.chrome, Enum.Material.Metal)
	Detail.plate(model, origin * CFrame.new(0, 4.0, 0.98), 2.6, 0.6, C.navy, C.white, Loc.get("prop.sampleRack"))
end

builders.quarantinePod = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- KARANTINA BOLMESI
	--
	-- Kogus padogundan farki cam on yuzu: bulasici hastayi gormen
	-- gerekiyor ama havasi ayri. Ic olcu `props.json` -> `shelter`
	-- (9,4 x 7,0) ve en buyuk hasta oraya siginca yerlesim denetimi
	-- geciyor; padoktaki kural burada da isliyor.
	p("Taban", Vector3.new(9.8, 0.4, 7.4), Vector3.new(0, 0.2, 0), C.white)
	d("TabanPahi", Vector3.new(10, 0.16, 7.6), Vector3.new(0, 0.42, 0), C.shadowWhite)
	d("Yatak", Vector3.new(6.6, 0.45, 4.8), Vector3.new(0, 0.64, -0.3), C.pad, Enum.Material.Fabric)
	d("Battaniye", Vector3.new(4.6, 0.12, 3.2), Vector3.new(0.5, 0.88, -0.1), C.towel, Enum.Material.Fabric)

	p("ArkaDuvar", Vector3.new(9.8, 6.2, 0.24), Vector3.new(0, 3.3, -3.7), C.white)
	p("YanDuvar", Vector3.new(0.24, 6.2, 7.4), Vector3.new(-4.8, 3.3, 0), C.white)
	p("YanDuvar", Vector3.new(0.24, 6.2, 7.4), Vector3.new(4.8, 3.3, 0), C.white)
	p("Tavan", Vector3.new(9.8, 0.3, 7.4), Vector3.new(0, 6.55, 0), C.offWhite)

	-- On yuz: parmaklik degil CAM. Karantinanin anlami havanin
	-- ayrilmasi; parmaklikli bir bolme bunu anlatmazdi.
	d("CamKanat", Vector3.new(4.5, 5.6, 0.16), Vector3.new(-2.5, 3.3, 3.7), C.glass, Enum.Material.Glass)
	d("CamKanat", Vector3.new(4.5, 5.6, 0.16), Vector3.new(2.5, 3.3, 3.7), C.glass, Enum.Material.Glass)
	d("CamCercevesi", Vector3.new(9.8, 0.3, 0.3), Vector3.new(0, 6.2, 3.7), C.deepTeal, Enum.Material.Metal)
	d("CamCercevesi", Vector3.new(9.8, 0.3, 0.3), Vector3.new(0, 0.5, 3.7), C.deepTeal, Enum.Material.Metal)
	d("CamDikmesi", Vector3.new(0.3, 5.6, 0.3), Vector3.new(0, 3.3, 3.7), C.deepTeal, Enum.Material.Metal)
	d("Kulp", Vector3.new(0.22, 1.4, 0.22), Vector3.new(0.6, 3.3, 3.9), C.chrome, Enum.Material.Metal)

	-- Hava kilidi: ustteki filtre kutusu ve borusu. Bolmenin neden
	-- "karantina" oldugunu anlatan parca bu.
	d("FiltreKutusu", Vector3.new(2.6, 1.1, 1.8), Vector3.new(-2.6, 7.2, -1), C.darkSteel, Enum.Material.Metal)
	d("FiltreIzgarasi", Vector3.new(2.2, 0.16, 1.4), Vector3.new(-2.6, 6.66, -1), C.steel, Enum.Material.Metal)
	Build.pillar({
		name = "HavaBorusu", size = Vector3.new(0.9, 1.4, 0.9),
		cf = origin * CFrame.new(-2.6, 7.9, -1), color = C.steel,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	d("UyariSeridi", Vector3.new(9.8, 0.4, 0.12), Vector3.new(0, 6.85, 3.6), C.gold)
	Detail.plate(model, origin * CFrame.new(2.8, 6.9, 3.6), 3.0, 0.8, C.accent, C.white, Loc.get("prop.quarantinePod"))
	Build.pillar({
		name = "SuKabi", size = Vector3.new(1.3, 0.45, 1.3),
		cf = origin * CFrame.new(3.2, 0.62, 1.9), color = C.water,
		canCollide = false, castShadow = false, parent = model,
	})
end

builders.uvLamp = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Duvara asili dezenfeksiyon lambasi. Isik KAYNAGI degil: klinikte
	-- isik sayisi bilerek az tutuluyor, bu yuzden mor parlaklik
	-- parcanin kendi `Neon` yuzeyinden geliyor.
	p("Govde", Vector3.new(2.8, 0.9, 0.7), Vector3.new(0, 0, -0.5), C.offWhite)
	Detail.trim(model, origin * CFrame.new(0, 0, -0.5), Vector3.new(2.8, 0.9, 0.7), 0.1, C.steel)
	d("Reflektor", Vector3.new(2.6, 0.7, 0.5), Vector3.new(0, -0.05, -0.1), C.chrome, Enum.Material.Metal)
	for index = 0, 1 do
		Build.pillar({
			name = "Tup", size = Vector3.new(0.3, 2.3, 0.3),
			cf = origin * CFrame.new(-0.45 + index * 0.9, -0.2, 0.05) * CFrame.Angles(0, 0, math.rad(90)),
			color = Color3.fromRGB(176, 150, 232), material = Enum.Material.Neon,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	d("Aski", Vector3.new(0.4, 0.5, 0.5), Vector3.new(-1.2, 0.5, -0.6), C.darkSteel, Enum.Material.Metal)
	d("Aski", Vector3.new(0.4, 0.5, 0.5), Vector3.new(1.2, 0.5, -0.6), C.darkSteel, Enum.Material.Metal)
	d("Anahtar", Vector3.new(0.3, 0.45, 0.16), Vector3.new(1.15, -0.5, 0.1), C.teal)
	Detail.cable(model, (origin * CFrame.new(-1.3, 0.3, -0.6)).Position, (origin * CFrame.new(-1.3, 1.4, -0.7)).Position, 0.08, C.black)
end

builders.feedStation = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Paspas", Vector3.new(5, 0.18, 3), Vector3.new(0, 0.09, 0), C.deepTeal, Enum.Material.Fabric)
	d("PaspasKenari", Vector3.new(5.3, 0.1, 3.3), Vector3.new(0, 0.06, 0), C.teal, Enum.Material.Fabric)
	for _, entry in { { -1.2, C.accent }, { 1.2, C.water } } do
		local x = entry[1] :: number
		local color = entry[2] :: Color3
		Build.pillar({
			name = "Kase", size = Vector3.new(1.8, 0.7, 1.8),
			cf = origin * CFrame.new(x, 0.45, 0), color = color,
			canCollide = false, castShadow = false, parent = model,
		})
		Build.pillar({
			name = "KaseAgzi", size = Vector3.new(2, 0.16, 2),
			cf = origin * CFrame.new(x, 0.78, 0), color = C.white,
			canCollide = false, castShadow = false, parent = model,
		})
		Build.pillar({
			name = "KaseIci", size = Vector3.new(1.5, 0.1, 1.5),
			cf = origin * CFrame.new(x, 0.62, 0), color = C.shadowWhite,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	d("MamaCuvali", Vector3.new(1.4, 1.8, 0.9), Vector3.new(0, 0.9, -1.1), C.wood, Enum.Material.Fabric)
	d("CuvalEtiketi", Vector3.new(1, 0.7, 0.06), Vector3.new(0, 1.1, -0.64), C.accent)
end

builders.washTub = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Ayak", Vector3.new(5.5, 2.4, 3.5), Vector3.new(0, 1.2, 0), C.offWhite)
	Detail.panel(model, origin * CFrame.new(0, 1.3, 1.8), 4.4, 1.9, C.white, C.shadowWhite, C.darkSteel)
	p("Kuvet", Vector3.new(6, 1.6, 4), Vector3.new(0, 3.2, 0), C.white)
	d("KuvetIci", Vector3.new(5.4, 1.3, 3.4), Vector3.new(0, 3.35, 0), C.shadowWhite)
	d("Su", Vector3.new(5.3, 0.35, 3.3), Vector3.new(0, 3.6, 0), C.water, Enum.Material.Glass)
	Detail.rim(model, origin * CFrame.new(0, 4.02, 0), Vector3.new(6, 0.24, 4), 0.24, C.chrome)
	d("Gider", Vector3.new(0.35, 0.08, 0.35), Vector3.new(0, 2.9, 0), C.darkSteel, Enum.Material.Metal)
	Build.pillar({
		name = "DusDiregi", size = Vector3.new(0.2, 3.5, 0.2),
		cf = origin * CFrame.new(0, 5.6, -1.8), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	d("DusKolu", Vector3.new(0.18, 0.18, 1.3), Vector3.new(0, 7.2, -1.3), C.chrome, Enum.Material.Metal)
	Build.pillar({
		name = "DusBasi", size = Vector3.new(0.9, 0.25, 0.9),
		cf = origin * CFrame.new(0, 7.05, -0.75), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Detail.cable(model, (origin * CFrame.new(0.6, 5.4, -1.8)).Position, (origin * CFrame.new(1.9, 4.3, -0.6)).Position, 0.1, C.paleTeal)
	d("Sampuan", Vector3.new(0.45, 0.9, 0.45), Vector3.new(2.4, 4.5, 0.6), C.teal)
	d("Sampuan", Vector3.new(0.45, 0.8, 0.45), Vector3.new(2.4, 4.45, -0.2), C.accent)
	d("Firca", Vector3.new(0.8, 0.25, 0.4), Vector3.new(-2.4, 4.2, 0.8), C.wood, Enum.Material.Wood)
end

-- ── Disari ────────────────────────────────────────────────────────────

-- ── Lobi ──────────────────────────────────────────────────────────────
-- Lobi eskiden dort portal kemeri, birkac bank ve iki saksidan
-- ibaretti: oyunun ILK gordugu oda ve en bos odasiydi. Buradaki
-- esyalarin isi mekani "salon" yapmak - sutun, cesme, banko, heykel,
-- pankart. Hepsi diger esyalarla ayni butce sozlesmesinden geciyor
-- (`data/props.json`) ve `verify_layout` ic ice girmediklerini olcuyor.

builders.column = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Kaide: iki kademeli, kare. Silindirin dogrudan yere oturmasi
	-- sutunu "boru" gibi gosteriyordu.
	p("Kaide", Vector3.new(3.4, 0.5, 3.4), Vector3.new(0, 0.25, 0), C.shadowWhite, Enum.Material.Marble)
	p("Kaide", Vector3.new(2.9, 0.55, 2.9), Vector3.new(0, 0.78, 0), C.offWhite, Enum.Material.Marble)
	Build.pillar({
		name = "Govde", size = Vector3.new(2.3, 16.4, 2.3),
		cf = origin * CFrame.new(0, 9.25, 0), color = C.white,
		material = Enum.Material.Marble, parent = model,
	})
	-- Yivler: silindirin uzerine dizilmis ince cubuklar. Duz bir
	-- silindir uzaktan plastik boru gibi duruyor; yiv onu tasa
	-- ceviriyor.
	for index = 1, 8 do
		local angle = (index / 8) * math.pi * 2
		d("Yiv", Vector3.new(0.22, 15.6, 0.22),
			Vector3.new(math.cos(angle) * 1.08, 9.25, math.sin(angle) * 1.08),
			C.shadowWhite, Enum.Material.Marble)
	end
	d("Bilezik", Vector3.new(2.6, 0.3, 2.6), Vector3.new(0, 16.9, 0), C.deepTeal, Enum.Material.Marble)
	p("Baslik", Vector3.new(2.9, 0.6, 2.9), Vector3.new(0, 17.4, 0), C.offWhite, Enum.Material.Marble)
	p("Baslik", Vector3.new(3.4, 0.5, 3.4), Vector3.new(0, 17.95, 0), C.shadowWhite, Enum.Material.Marble)
	d("Tepe", Vector3.new(3.0, 0.4, 3.0), Vector3.new(0, 18.4, 0), C.deepTeal, Enum.Material.Marble)
end

builders.fountain = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Sekizgen havuz: sekiz duvar parcasi halka olusturuyor. Tek bir
	-- silindir kullanmak "havuz" degil "tabak" gorunumu veriyordu.
	p("Taban", Vector3.new(11.6, 0.4, 11.6), Vector3.new(0, 0.2, 0), C.shadowWhite, Enum.Material.Marble)
	for index = 1, 8 do
		local angle = (index / 8) * math.pi * 2
		p("HavuzDuvari", Vector3.new(4.6, 1.5, 0.7),
			Vector3.new(math.cos(angle) * 5.2, 0.95, math.sin(angle) * 5.2),
			C.white, Enum.Material.Marble, CFrame.Angles(0, -angle, 0))
		d("HavuzPahi", Vector3.new(4.8, 0.24, 0.95),
			Vector3.new(math.cos(angle) * 5.2, 1.74, math.sin(angle) * 5.2),
			C.deepTeal, Enum.Material.Marble, CFrame.Angles(0, -angle, 0))
	end
	-- Su yuzeyi: hafif saydam ve parlak. Hareketli bir doku asset
	-- isterdi; burada yuzey rengi ve parlaklik yetiyor.
	local water = d("Su", Vector3.new(10.2, 0.3, 10.2), Vector3.new(0, 1.35, 0), C.water, Enum.Material.Glass)
	water.Transparency = 0.3
	water.Reflectance = 0.25

	-- Ust kademe ve fiskiye
	Build.pillar({
		name = "Ayak", size = Vector3.new(1.6, 2.2, 1.6),
		cf = origin * CFrame.new(0, 2.5, 0), color = C.offWhite,
		material = Enum.Material.Marble, parent = model,
	})
	Build.pillar({
		name = "UstTas", size = Vector3.new(4.6, 0.45, 4.6),
		cf = origin * CFrame.new(0, 3.75, 0), color = C.white,
		material = Enum.Material.Marble, canCollide = false, castShadow = false, parent = model,
	})
	local upper = Build.pillar({
		name = "UstSu", size = Vector3.new(4.0, 0.22, 4.0),
		cf = origin * CFrame.new(0, 4.02, 0), color = C.water,
		material = Enum.Material.Glass, canCollide = false, castShadow = false, parent = model,
	})
	upper.Transparency = 0.28
	Build.pillar({
		name = "Fiskiye", size = Vector3.new(0.5, 2.0, 0.5),
		cf = origin * CFrame.new(0, 5.1, 0), color = C.paleTeal,
		material = Enum.Material.Neon, canCollide = false, castShadow = false, parent = model,
	})
	-- Kademeden dokulen su: dort ince serit.
	for index = 1, 4 do
		local angle = (index / 4) * math.pi * 2 + math.pi / 4
		local fall = d("Dokulen", Vector3.new(0.5, 2.3, 0.5),
			Vector3.new(math.cos(angle) * 1.9, 2.6, math.sin(angle) * 1.9),
			C.water, Enum.Material.Glass)
		fall.Transparency = 0.45
	end
end

builders.lobbyDesk = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Uc parcali banko: orta govde + iki kanat. Tek bir kutu "banko"
	-- degil "duvar" gibi duruyordu.
	p("Govde", Vector3.new(9.0, 3.4, 4.6), Vector3.new(0, 1.7, 0), C.wood, Enum.Material.Wood)
	p("Kanat", Vector3.new(3.4, 3.4, 4.0), Vector3.new(-6.0, 1.7, 0.3), C.wood,
		Enum.Material.Wood, CFrame.Angles(0, math.rad(18), 0))
	p("Kanat", Vector3.new(3.4, 3.4, 4.0), Vector3.new(6.0, 1.7, 0.3), C.wood,
		Enum.Material.Wood, CFrame.Angles(0, math.rad(-18), 0))
	Detail.trim(model, origin * CFrame.new(0, 1.7, 0), Vector3.new(9.0, 3.4, 4.6), 0.16, C.darkWood)
	-- Tezgah tablasi: govdenin uzerinden tasiyor.
	d("Tabla", Vector3.new(15.4, 0.34, 5.4), Vector3.new(0, 3.55, 0.1), C.offWhite, Enum.Material.Marble)
	d("TablaPahi", Vector3.new(15.8, 0.16, 5.8), Vector3.new(0, 3.36, 0.1), C.deepTeal, Enum.Material.Marble)
	Detail.drawer(model, origin * CFrame.new(-2.4, 1.9, 2.32), 3.4, 1.0, C.midWood, C.chrome)
	Detail.drawer(model, origin * CFrame.new(2.4, 1.9, 2.32), 3.4, 1.0, C.midWood, C.chrome)
	Detail.screen(model, origin * CFrame.new(-3.2, 4.5, -0.4) * CFrame.Angles(math.rad(-12), math.rad(14), 0),
		3.0, 1.9, C.black, C.screen, Loc.get("lobby.title"))
	d("Klavye", Vector3.new(2.6, 0.14, 1.0), Vector3.new(-3.2, 3.74, 1.3), C.black)
	-- Zil ve kalemlik: bankoyu "kullanilan bir yer" yapan detaylar.
	Build.pillar({
		name = "Zil", size = Vector3.new(0.9, 0.5, 0.9),
		cf = origin * CFrame.new(3.4, 3.95, 1.2), color = C.chrome,
		material = Enum.Material.Metal, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Kalemlik", size = Vector3.new(0.8, 1.0, 0.8),
		cf = origin * CFrame.new(4.8, 4.2, 0.6), color = C.deepTeal,
		canCollide = false, castShadow = false, parent = model,
	})
	Detail.plate(model, origin * CFrame.new(0, 2.4, 2.42), 6.4, 1.2, C.deepTeal, C.white, Loc.get("lobby.desk"))
end

builders.banner = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Tavandan sarkan pankart: cubuk + kumas + puskuller.
	p("Cubuk", Vector3.new(5.0, 0.3, 0.3), Vector3.new(0, 5.2, 0), C.darkSteel, Enum.Material.Metal)
	d("CubukUcu", Vector3.new(0.5, 0.5, 0.5), Vector3.new(-2.5, 5.2, 0), C.gold, Enum.Material.Metal)
	d("CubukUcu", Vector3.new(0.5, 0.5, 0.5), Vector3.new(2.5, 5.2, 0), C.gold, Enum.Material.Metal)
	d("Kumas", Vector3.new(4.4, 8.4, 0.14), Vector3.new(0, 0.9, 0), C.deepTeal, Enum.Material.Fabric)
	d("KumasKenari", Vector3.new(4.7, 0.3, 0.2), Vector3.new(0, 5.0, 0), C.gold, Enum.Material.Fabric)
	d("KumasKenari", Vector3.new(4.7, 0.3, 0.2), Vector3.new(0, -3.2, 0), C.gold, Enum.Material.Fabric)
	-- Alt kenardaki uc puskul: kumasin agirligini gosteriyor.
	for index = -1, 1 do
		d("Puskul", Vector3.new(0.3, 0.9, 0.3), Vector3.new(index * 1.6, -3.8, 0), C.gold, Enum.Material.Fabric)
	end
	Detail.plate(model, origin * CFrame.new(0, 1.6, 0.12), 3.8, 2.4, C.deepTeal, C.white, Loc.get("lobby.title"))
end

builders.noticeBoard = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Cerceve", Vector3.new(6.6, 5.2, 0.36), Vector3.new(0, 0, 0), C.darkWood, Enum.Material.Wood)
	d("Mantar", Vector3.new(6.0, 4.6, 0.14), Vector3.new(0, 0, 0.16), C.sand, Enum.Material.Fabric)
	Detail.trim(model, origin, Vector3.new(6.6, 5.2, 0.36), 0.14, C.midWood)
	-- Asili notlar: farkli boy ve renkte kagitlar, hafif egik.
	local notes = {
		{ -1.9, 1.0, 1.7, 1.2, C.white, 6 },
		{ 0.2, 1.2, 1.5, 1.0, C.towel, -4 },
		{ 2.0, 0.8, 1.6, 1.4, C.white, 3 },
		{ -1.4, -1.2, 1.9, 1.1, C.paleTeal, -7 },
		{ 1.5, -1.3, 1.6, 1.2, C.white, 5 },
	}
	for _, note in notes do
		local x, y, w, h, tint, tilt = note[1], note[2], note[3], note[4], note[5], note[6]
		d("Not", Vector3.new(w :: number, h :: number, 0.06),
			Vector3.new(x :: number, y :: number, 0.26), tint :: Color3,
			Enum.Material.SmoothPlastic, CFrame.Angles(0, 0, math.rad(tilt :: number)))
		d("RaptiyeBasi", Vector3.new(0.18, 0.18, 0.1),
			Vector3.new(x :: number, (y :: number) + (h :: number) * 0.36, 0.32), C.accent, Enum.Material.Metal)
	end
	Detail.plate(model, origin * CFrame.new(0, 2.9, 0.1), 4.2, 0.9, C.navy, C.white, Loc.get("lobby.notice"))
end

builders.statue = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Kaide
	p("Kaide", Vector3.new(4.8, 0.5, 4.8), Vector3.new(0, 0.25, 0), C.shadowWhite, Enum.Material.Marble)
	p("Kaide", Vector3.new(4.0, 3.6, 4.0), Vector3.new(0, 2.3, 0), C.offWhite, Enum.Material.Marble)
	p("Kaide", Vector3.new(4.6, 0.4, 4.6), Vector3.new(0, 4.3, 0), C.shadowWhite, Enum.Material.Marble)
	Detail.plate(model, origin * CFrame.new(0, 2.4, 2.06), 3.2, 1.0, C.navy, C.gold, Loc.get("lobby.statue"))

	-- Oturan kopek: veteriner klinigi lobisine yakisan tek heykel.
	-- Govde, gogus, kafa, kulaklar, on ayaklar, kuyruk.
	local stone = C.white
	d("Govde", Vector3.new(1.7, 2.0, 2.6), Vector3.new(0, 5.6, -0.3), stone, Enum.Material.Marble,
		CFrame.Angles(math.rad(-12), 0, 0))
	d("Gogus", Vector3.new(1.5, 1.6, 1.4), Vector3.new(0, 6.1, 0.7), stone, Enum.Material.Marble)
	d("Kafa", Vector3.new(1.3, 1.25, 1.4), Vector3.new(0, 7.5, 0.75), stone, Enum.Material.Marble)
	d("Burun", Vector3.new(0.75, 0.6, 0.8), Vector3.new(0, 7.3, 1.5), stone, Enum.Material.Marble)
	d("BurunUcu", Vector3.new(0.35, 0.3, 0.25), Vector3.new(0, 7.4, 1.9), C.shadowWhite, Enum.Material.Marble)
	for _, side in { -1, 1 } do
		d("Kulak", Vector3.new(0.4, 0.9, 0.55), Vector3.new(side * 0.55, 8.2, 0.6), stone,
			Enum.Material.Marble, CFrame.Angles(0, 0, math.rad(side * 14)))
		d("OnAyak", Vector3.new(0.45, 1.5, 0.5), Vector3.new(side * 0.5, 5.2, 1.15), stone, Enum.Material.Marble)
		d("Pati", Vector3.new(0.55, 0.35, 0.9), Vector3.new(side * 0.5, 4.65, 1.35), stone, Enum.Material.Marble)
	end
	d("Kuyruk", Vector3.new(0.4, 0.4, 1.8), Vector3.new(0, 4.9, -1.5), stone, Enum.Material.Marble,
		CFrame.Angles(math.rad(22), 0, 0))
end

builders.rugRunner = function(model, origin)
	local d = makerDecor(model, origin)
	-- Uzun hali yolu: giristen salona kadar gidiyor ve gozu ileri
	-- cekiyor. Zeminden 0,1 stud yukarida duruyor ki z-fighting olmasin.
	-- Hali zemin KAROLARININ ustunde duruyor. Karolar 0..0,06
	-- arasinda; hali 0,06'dan basliyor, yani icine girmiyor.
	d("HaliKenari", Vector3.new(10.0, 0.1, 20.0), Vector3.new(0, 0.11, 0), C.deepTeal, Enum.Material.Fabric)
	d("Hali", Vector3.new(9.4, 0.12, 19.4), Vector3.new(0, 0.22, 0), C.carpet, Enum.Material.Fabric)
	-- Boyuna iki serit ve enine desen cizgileri.
	for _, side in { -1, 1 } do
		d("HaliSeridi", Vector3.new(0.5, 0.14, 19.0), Vector3.new(side * 3.6, 0.29, 0), C.gold, Enum.Material.Fabric)
	end
	for index = -4, 4 do
		d("HaliDeseni", Vector3.new(7.8, 0.14, 0.24), Vector3.new(0, 0.29, index * 2.2), C.paleTeal, Enum.Material.Fabric)
	end
end

builders.lobbySofa = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Govde", Vector3.new(8.4, 1.4, 3.4), Vector3.new(0, 1.5, 0), C.navy, Enum.Material.Fabric)
	d("Minder", Vector3.new(8.0, 0.6, 3.0), Vector3.new(0, 2.4, 0.1), C.pad, Enum.Material.Fabric)
	d("Sirtlik", Vector3.new(8.4, 2.4, 0.7), Vector3.new(0, 3.0, -1.4), C.navy, Enum.Material.Fabric)
	d("SirtlikPahi", Vector3.new(8.4, 0.35, 0.9), Vector3.new(0, 4.15, -1.4), C.pad, Enum.Material.Fabric)
	for _, side in { -1, 1 } do
		d("Kolcak", Vector3.new(0.7, 1.5, 3.4), Vector3.new(side * 3.85, 2.6, 0), C.navy, Enum.Material.Fabric)
		d("KolcakPahi", Vector3.new(0.9, 0.3, 3.6), Vector3.new(side * 3.85, 3.4, 0), C.pad, Enum.Material.Fabric)
	end
	-- Yastiklar ve ahsap ayaklar
	for _, side in { -1, 1 } do
		d("Yastik", Vector3.new(1.5, 1.4, 0.4), Vector3.new(side * 2.4, 3.1, -0.9), C.paleTeal,
			Enum.Material.Fabric, CFrame.Angles(math.rad(-14), 0, 0))
	end
	for _, sx in { -1, 1 } do
		for _, sz in { -1, 1 } do
			Detail.leg(model, origin * CFrame.new(sx * 3.6, 0.8, sz * 1.2), 0.8, 0.36, C.darkWood)
		end
	end
end

builders.planterBox = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Tekne", Vector3.new(9.4, 2.2, 2.8), Vector3.new(0, 1.1, 0), C.terracotta)
	Detail.trim(model, origin * CFrame.new(0, 1.1, 0), Vector3.new(9.4, 2.2, 2.8), 0.16, C.darkWood)
	d("Toprak", Vector3.new(8.8, 0.3, 2.3), Vector3.new(0, 2.25, 0), C.soil)
	-- Uc calilik: her biri birkac yaprak kumesi. Tek bir yesil kutu
	-- "bitki" degil "kutu" gorunumu veriyordu.
	for index = -1, 1 do
		local x = index * 3.0
		Build.pillar({
			name = "Calilik", size = Vector3.new(2.4, 1.8, 2.2),
			cf = origin * CFrame.new(x, 3.1, 0), color = C.leaf,
			canCollide = false, castShadow = false, parent = model,
		})
		d("Yaprak", Vector3.new(1.4, 0.9, 1.3), Vector3.new(x - 0.7, 3.5, 0.4), C.midLeaf,
			Enum.Material.Grass, CFrame.Angles(0, math.rad(24), math.rad(12)))
		d("Yaprak", Vector3.new(1.3, 0.9, 1.2), Vector3.new(x + 0.7, 3.4, -0.3), C.darkLeaf,
			Enum.Material.Grass, CFrame.Angles(0, math.rad(-18), math.rad(-10)))
	end
	d("Cicek", Vector3.new(0.5, 0.5, 0.5), Vector3.new(-1.5, 3.9, 0.5), C.blossom)
	d("Cicek", Vector3.new(0.5, 0.5, 0.5), Vector3.new(2.2, 3.8, -0.4), C.gold)
end

builders.archFrame = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Portal kemeri: iki ayak + basamakli kemer + tepe tasi. Kemer
	-- egrisi kademeli bloklardan kuruluyor; Roblox'ta egri bir parca
	-- mesh isterdi ve bu ortamda mesh dogrulanamaz.
	for _, side in { -1, 1 } do
		p("KemerAyagi", Vector3.new(1.8, 11.0, 1.8), Vector3.new(side * 5.4, 5.5, 0), C.offWhite, Enum.Material.Marble)
		d("AyakTabani", Vector3.new(2.4, 0.6, 2.4), Vector3.new(side * 5.4, 0.3, 0), C.shadowWhite, Enum.Material.Marble)
		d("AyakBasligi", Vector3.new(2.3, 0.55, 2.3), Vector3.new(side * 5.4, 11.2, 0), C.shadowWhite, Enum.Material.Marble)
	end
	-- Kemerin kademeleri
	local steps = { { 4.3, 12.1, 1.8 }, { 3.0, 12.9, 1.7 }, { 1.5, 13.5, 1.6 } }
	for _, step in steps do
		local x, y, w = step[1], step[2], step[3]
		for _, side in { -1, 1 } do
			d("KemerTasi", Vector3.new(w, 1.0, 1.7), Vector3.new(side * x, y, 0), C.white, Enum.Material.Marble)
		end
	end
	d("KilitTasi", Vector3.new(2.0, 1.3, 1.9), Vector3.new(0, 13.9, 0), C.deepTeal, Enum.Material.Marble)
	d("KemerAlni", Vector3.new(12.2, 0.5, 1.5), Vector3.new(0, 14.6, 0), C.shadowWhite, Enum.Material.Marble)
end

-- ── Klinik cephesi ve ic mekan detaylari ─────────────────────────────
-- Bina disaridan duz bir kutuydu: kapi bir delik, cevresi cimendi.
-- Buradaki esyalar binaya BIR CEPHE veriyor - sacak, basamaklar,
-- bayrak diregi, ambulans - ve icerde odalari birbirinden ayiran
-- yonlendirme tabelalari.

builders.entranceCanopy = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Giris sacagi: kapinin onunde, iki direk uzerinde duran genis bir
	-- ortu. Yagmurda beklenen yer burasi ve bina "girisi olan bir bina"
	-- gibi duruyor.
	for _, side in { -1, 1 } do
		Build.pillar({
			name = "SacakDiregi", size = Vector3.new(1.3, 8.4, 1.3),
			cf = origin * CFrame.new(side * 9.4, 4.2, 4.6), color = C.steel,
			material = Enum.Material.Metal, parent = model,
		})
		d("DirekTabani", Vector3.new(2.0, 0.5, 2.0), Vector3.new(side * 9.4, 0.25, 4.6), C.darkSteel, Enum.Material.Metal)
		d("DirekBasligi", Vector3.new(1.7, 0.4, 1.7), Vector3.new(side * 9.4, 8.5, 4.6), C.darkSteel, Enum.Material.Metal)
		-- Capraz destek: sacagin havada durmasini ACIKLAYAN parca.
		d("Destek", Vector3.new(0.4, 3.6, 0.4), Vector3.new(side * 8.2, 7.4, 2.6), C.darkSteel,
			Enum.Material.Metal, CFrame.Angles(math.rad(38), 0, math.rad(side * -20)))
	end
	p("Sacak", Vector3.new(21.0, 0.7, 12.0), Vector3.new(0, 8.9, 0.6), C.deepTeal)
	d("SacakAlni", Vector3.new(21.4, 1.1, 0.5), Vector3.new(0, 8.6, 6.4), C.white)
	d("SacakAlni", Vector3.new(0.5, 1.1, 12.2), Vector3.new(-10.6, 8.6, 0.6), C.white)
	d("SacakAlni", Vector3.new(0.5, 1.1, 12.2), Vector3.new(10.6, 8.6, 0.6), C.white)
	-- Alt yuzey seritleri: duz bir tavan yerine kaplama izlenimi.
	for index = -4, 4 do
		d("SacakKaplamasi", Vector3.new(1.6, 0.16, 11.4), Vector3.new(index * 2.2, 8.48, 0.6), C.offWhite)
	end
	Detail.plate(model, origin * CFrame.new(0, 8.6, 6.68), 9.0, 1.5, C.deepTeal, C.white, Loc.get("clinic.name"))
	-- Sacak altindaki iki lamba
	for _, side in { -1, 1 } do
		local bulb = d("SacakLambasi", Vector3.new(1.6, 0.3, 1.6), Vector3.new(side * 5.0, 8.35, 2.4), C.white, Enum.Material.Neon)
		bulb.Name = "SacakLambasi"
	end
end

builders.entranceSteps = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Uc basamak: bina zemini ile bahce arasindaki gecis. Kapinin
	-- dogrudan cimene acilmasi binayi "yere konmus" gosteriyordu.
	for index = 0, 2 do
		local depth = 4.6 - index * 1.4
		p("Basamak", Vector3.new(19.4 - index * 1.2, 0.42, depth),
			Vector3.new(0, 0.21 + index * 0.42, -index * 0.7), C.shadowWhite, Enum.Material.Concrete)
		d("BasamakPahi", Vector3.new(19.6 - index * 1.2, 0.14, depth + 0.12),
			Vector3.new(0, 0.44 + index * 0.42, -index * 0.7), C.offWhite, Enum.Material.Concrete)
	end
	-- Iki yanda korkuluk
	for _, side in { -1, 1 } do
		d("Korkuluk", Vector3.new(0.24, 0.24, 5.0), Vector3.new(side * 9.2, 1.9, 0.2), C.chrome,
			Enum.Material.Metal, CFrame.Angles(math.rad(-14), 0, 0))
		d("KorkulukDikmesi", Vector3.new(0.2, 1.6, 0.2), Vector3.new(side * 9.2, 1.0, 2.2), C.chrome, Enum.Material.Metal)
		d("KorkulukDikmesi", Vector3.new(0.2, 2.2, 0.2), Vector3.new(side * 9.2, 1.3, -1.6), C.chrome, Enum.Material.Metal)
	end
	-- Tekerlekli sandalye rampasi: kapiya ulasmanin ikinci yolu.
	d("Rampa", Vector3.new(3.4, 0.3, 5.2), Vector3.new(-11.6, 0.62, 0), C.shadowWhite,
		Enum.Material.Concrete, CFrame.Angles(math.rad(-9), 0, 0))
end

builders.flagPole = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Kaide", Vector3.new(2.2, 0.7, 2.2), Vector3.new(0, 0.35, 0), C.shadowWhite, Enum.Material.Concrete)
	d("KaidePahi", Vector3.new(2.5, 0.2, 2.5), Vector3.new(0, 0.75, 0), C.offWhite, Enum.Material.Concrete)
	Build.pillar({
		name = "Direk", size = Vector3.new(0.5, 18.0, 0.5),
		cf = origin * CFrame.new(0, 9.4, 0), color = C.chrome,
		material = Enum.Material.Metal, parent = model,
	})
	d("Tepe", Vector3.new(0.7, 0.7, 0.7), Vector3.new(0, 18.6, 0), C.gold, Enum.Material.Metal)
	-- Bayrak: klinigin renkleri. Ucusma animasyonu yok; kumas kademeli
	-- parcalarla dalgali duruyor.
	for index = 0, 4 do
		d("Bayrak", Vector3.new(1.5, 3.4, 0.12),
			Vector3.new(1.0 + index * 1.45, 16.2, math.sin(index * 0.9) * 0.35), C.deepTeal,
			Enum.Material.Fabric, CFrame.Angles(0, math.rad(math.sin(index * 0.9) * 14), 0))
	end
	d("BayrakPatisi", Vector3.new(1.4, 1.4, 0.16), Vector3.new(4.2, 16.2, 0.1), C.white, Enum.Material.Fabric)
	d("Halat", Vector3.new(0.1, 9.0, 0.1), Vector3.new(-0.3, 12.0, 0), C.towel)
end

builders.ambulance = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Hayvan ambulansi: acil vakalarin nereden geldigini anlatan tek
	-- parca. Klinigin onunde park halinde duruyor.
	p("Kasa", Vector3.new(6.6, 4.4, 9.0), Vector3.new(0, 3.6, -2.0), C.white)
	p("Kabin", Vector3.new(6.4, 3.2, 4.6), Vector3.new(0, 3.0, 4.6), C.white)
	d("On", Vector3.new(6.2, 1.6, 1.4), Vector3.new(0, 1.9, 6.9), C.white)
	d("OnCam", Vector3.new(5.6, 2.0, 0.3), Vector3.new(0, 3.6, 6.85), C.glass, Enum.Material.Glass)
	d("YanCam", Vector3.new(0.3, 1.5, 2.2), Vector3.new(-3.2, 3.6, 4.6), C.glass, Enum.Material.Glass)
	d("YanCam", Vector3.new(0.3, 1.5, 2.2), Vector3.new(3.2, 3.6, 4.6), C.glass, Enum.Material.Glass)
	d("ArkaKapi", Vector3.new(6.0, 3.6, 0.3), Vector3.new(0, 3.4, -6.4), C.shadowWhite)
	d("Serit", Vector3.new(6.8, 0.9, 9.2), Vector3.new(0, 2.6, -2.0), C.accent)
	d("Serit", Vector3.new(6.6, 0.5, 4.8), Vector3.new(0, 2.6, 4.6), C.accent)
	-- Tepe lambasi
	d("TepeLambasi", Vector3.new(3.6, 0.6, 1.0), Vector3.new(0, 5.9, 3.6), C.accent, Enum.Material.Neon)
	d("TepeLambasi", Vector3.new(1.2, 0.6, 1.0), Vector3.new(0, 5.9, 3.6), C.water, Enum.Material.Neon)
	-- Tekerlekler
	for _, sx in { -1, 1 } do
		for _, sz in { 4.2, -3.6 } do
			Build.pillar({
				name = "Teker", size = Vector3.new(2.2, 0.9, 2.2),
				cf = origin * CFrame.new(sx * 3.1, 1.1, sz) * CFrame.Angles(0, 0, math.rad(90)),
				color = C.rubber, canCollide = false, castShadow = false, parent = model,
			})
			Build.pillar({
				name = "Jant", size = Vector3.new(1.2, 1.0, 1.2),
				cf = origin * CFrame.new(sx * 3.1, 1.1, sz) * CFrame.Angles(0, 0, math.rad(90)),
				color = C.chrome, material = Enum.Material.Metal,
				canCollide = false, castShadow = false, parent = model,
			})
		end
	end
	Detail.plate(model, origin * CFrame.new(3.32, 3.4, -2.0) * CFrame.Angles(0, math.rad(90), 0),
		6.0, 1.4, C.deepTeal, C.white, Loc.get("clinic.name"))
end

builders.bikeRack = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	p("Ray", Vector3.new(6.4, 0.3, 0.3), Vector3.new(0, 0.6, -1.0), C.darkSteel, Enum.Material.Metal)
	p("Ray", Vector3.new(6.4, 0.3, 0.3), Vector3.new(0, 0.6, 1.0), C.darkSteel, Enum.Material.Metal)
	for index = -2, 2 do
		-- U seklinde bisiklet yuvasi: iki dikme + ust kavis.
		d("Yuva", Vector3.new(0.22, 2.6, 0.22), Vector3.new(index * 1.5, 1.4, -1.0), C.chrome, Enum.Material.Metal)
		d("Yuva", Vector3.new(0.22, 2.6, 0.22), Vector3.new(index * 1.5, 1.4, 1.0), C.chrome, Enum.Material.Metal)
		d("YuvaKavisi", Vector3.new(0.22, 0.22, 2.2), Vector3.new(index * 1.5, 2.65, 0), C.chrome, Enum.Material.Metal)
	end
	d("Zemin", Vector3.new(6.8, 0.14, 3.0), Vector3.new(0, 0.07, 0), C.shadowWhite, Enum.Material.Concrete)
end

builders.roomSign = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Oda yonlendirme tabelasi: kapinin ustunde, iki yuzu de yazili.
	-- Klinikte "hangi oda neresi" sorusunun cevabi buydu; onceden yoktu
	-- ve oyuncu odalari ezberlemek zorundaydi.
	p("Govde", Vector3.new(5.2, 1.7, 0.34), Vector3.new(0, 0, 0), C.deepTeal)
	Detail.trim(model, origin, Vector3.new(5.2, 1.7, 0.34), 0.12, C.chrome)
	d("Aski", Vector3.new(0.16, 1.1, 0.16), Vector3.new(-1.8, 1.3, 0), C.darkSteel, Enum.Material.Metal)
	d("Aski", Vector3.new(0.16, 1.1, 0.16), Vector3.new(1.8, 1.3, 0), C.darkSteel, Enum.Material.Metal)
	d("AskiKirisi", Vector3.new(4.2, 0.16, 0.16), Vector3.new(0, 1.85, 0), C.darkSteel, Enum.Material.Metal)
	-- Ok isareti: tabelanin yon gosterdigini anlatiyor.
	d("Ok", Vector3.new(0.9, 0.22, 0.1), Vector3.new(-1.9, 0, 0.2), C.white)
	d("OkUcu", Vector3.new(0.35, 0.35, 0.1), Vector3.new(-2.3, 0, 0.2), C.white, nil, CFrame.Angles(0, 0, math.rad(45)))
	Build.surfaceText(
		Build.part({
			name = "Yuzey",
			size = Vector3.new(4.6, 1.2, 0.08),
			cf = origin * CFrame.new(0.4, 0, 0.2),
			color = C.deepTeal,
			canCollide = false,
			castShadow = false,
			parent = model,
		}),
		Enum.NormalId.Front,
		Loc.get("prop.roomSign"),
		C.white
	)
end

builders.wallRail = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Duvar korkulugu (lambri kusagi): koridor duvarlarini yatay olarak
	-- boluyor. Hastane duvarlarinin en taniyan detayi bu serittir.
	p("Kusak", Vector3.new(11.6, 0.55, 0.3), Vector3.new(0, 0, 0), C.darkWood, Enum.Material.Wood)
	d("KusakUstu", Vector3.new(11.8, 0.18, 0.42), Vector3.new(0, 0.32, 0.04), C.wood, Enum.Material.Wood)
	d("KusakAlti", Vector3.new(11.8, 0.14, 0.38), Vector3.new(0, -0.3, 0.02), C.wood, Enum.Material.Wood)
	-- Tamponlar: sedye carpmasin diye. Duz bir tahta yerine islevi
	-- gorunen bir parca.
	for index = -2, 2 do
		d("Tampon", Vector3.new(1.6, 0.42, 0.16), Vector3.new(index * 2.3, 0, 0.2), C.deepTeal, Enum.Material.Fabric)
	end
	for index = -1, 1 do
		d("Konsol", Vector3.new(0.26, 0.5, 0.28), Vector3.new(index * 4.4, -0.1, -0.16), C.darkSteel, Enum.Material.Metal)
	end
end

builders.magazineTable = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	-- Bekleme odasi sehpasi: dergiler ve bir su bardagi. Bekleme
	-- odasinin "burada beklenir" demesini saglayan detay.
	d("Tabla", Vector3.new(4.4, 0.3, 4.4), Vector3.new(0, 2.3, 0), C.wood, Enum.Material.Wood)
	Detail.trim(model, origin * CFrame.new(0, 2.3, 0), Vector3.new(4.4, 0.3, 4.4), 0.12, C.darkWood)
	d("AltRaf", Vector3.new(3.6, 0.22, 3.6), Vector3.new(0, 0.9, 0), C.midWood, Enum.Material.Wood)
	for _, sx in { -1, 1 } do
		for _, sz in { -1, 1 } do
			Detail.leg(model, origin * CFrame.new(sx * 1.8, 2.15, sz * 1.8), 2.15, 0.3, C.darkWood)
		end
	end
	-- Ust uste dizilmis dergiler: her biri biraz kaymis.
	local tints = { C.accent, C.water, C.leaf, C.gold }
	for index = 0, 3 do
		d("Dergi", Vector3.new(1.9, 0.1, 1.4),
			Vector3.new(-0.7 + index * 0.12, 2.5 + index * 0.11, 0.4 - index * 0.1),
			tints[index + 1], Enum.Material.SmoothPlastic,
			CFrame.Angles(0, math.rad(index * 7 - 10), 0))
	end
	Build.pillar({
		name = "Bardak", size = Vector3.new(0.7, 0.9, 0.7),
		cf = origin * CFrame.new(1.4, 2.9, -0.9), color = C.water,
		material = Enum.Material.Glass, canCollide = false, castShadow = false, parent = model,
	})
	d("AltRafDergi", Vector3.new(2.6, 0.14, 1.8), Vector3.new(0.2, 1.08, 0), C.towel)
end

builders.clinicSign = function(model, origin)
	local p, d = maker(model, origin), makerDecor(model, origin)
	for _, side in { -7, 7 } do
		Build.pillar({
			name = "Direk", size = Vector3.new(0.55, 12, 0.55),
			cf = origin * CFrame.new(side, 6, 0), color = C.darkSteel,
			material = Enum.Material.Metal, parent = model,
		})
		d("DirekTabani", Vector3.new(1.2, 0.5, 1.2), Vector3.new(side, 0.25, 0), C.steel, Enum.Material.Metal)
		Detail.bolt(model, origin * CFrame.new(side, 11.6, 0.3), 0.35, C.chrome)
	end
	p("Tabela", Vector3.new(16, 4, 0.45), Vector3.new(0, 12.5, 0), C.teal)
	d("CerceveUst", Vector3.new(16.8, 0.4, 0.6), Vector3.new(0, 14.7, 0), C.white)
	d("CerceveAlt", Vector3.new(16.8, 0.4, 0.6), Vector3.new(0, 10.3, 0), C.white)
	d("CerceveSol", Vector3.new(0.4, 4.8, 0.6), Vector3.new(-8.2, 12.5, 0), C.white)
	d("CerceveSag", Vector3.new(0.4, 4.8, 0.6), Vector3.new(8.2, 12.5, 0), C.white)
	local board = d("TabelaYuzu", Vector3.new(15.4, 3.4, 0.2), Vector3.new(0, 12.5, -0.3), C.deepTeal)
	Build.surfaceText(board, Enum.NormalId.Back, Loc.get("clinic.name"), C.white)

	-- Veteriner haci: gercekten isiyan bir tabela isigi, Neon hakli.
	d("HacDikey", Vector3.new(0.9, 2.6, 0.2), Vector3.new(-6.4, 12.5, -0.45), C.white, Enum.Material.Neon)
	d("HacYatay", Vector3.new(2.6, 0.9, 0.2), Vector3.new(-6.4, 12.5, -0.45), C.white, Enum.Material.Neon)
end

builders.lampPost = function(model, origin)
	local d = makerDecor(model, origin)
	local lamp = SkyLighting.lamp("lampPost")
	Build.pillar({
		name = "Taban", size = Vector3.new(1.5, 0.6, 1.5),
		cf = origin * CFrame.new(0, 0.3, 0), color = C.darkSteel,
		material = Enum.Material.Metal, parent = model,
	})
	Build.pillar({
		name = "Direk", size = Vector3.new(0.45, 11, 0.45),
		cf = origin * CFrame.new(0, 5.8, 0), color = C.navy,
		material = Enum.Material.Metal, parent = model,
	})
	d("Bilezik", Vector3.new(0.62, 0.22, 0.62), Vector3.new(0, 1.4, 0), C.chrome, Enum.Material.Metal)
	d("Bilezik", Vector3.new(0.62, 0.22, 0.62), Vector3.new(0, 10.6, 0), C.chrome, Enum.Material.Metal)
	d("FenerKasasi", Vector3.new(1.9, 0.25, 1.9), Vector3.new(0, 12.3, 0), C.navy, Enum.Material.Metal)
	d("FenerTepesi", Vector3.new(1.2, 0.5, 1.2), Vector3.new(0, 12.6, 0), C.navy, Enum.Material.Metal)
	local head = d("Fener", Vector3.new(1.6, 1.3, 1.6), Vector3.new(0, 11.5, 0), lamp.color, Enum.Material.Neon)
	for _, corner in { Vector3.new(0.8, 11.5, 0.8), Vector3.new(-0.8, 11.5, 0.8), Vector3.new(0.8, 11.5, -0.8), Vector3.new(-0.8, 11.5, -0.8) } do
		d("FenerDikmesi", Vector3.new(0.12, 1.5, 0.12), corner, C.navy, Enum.Material.Metal)
	end
	-- Adi SABIT: DayCycle sokak lambalarini bu adla buluyor ve geceyle
	-- birlikte yakiyor. Ad degisirse lambalar gece yanmaz.
	local light = Instance.new("PointLight")
	light.Name = "SokakLambasiIsik"
	light.Brightness = lamp.brightness
	light.Range = lamp.range
	light.Color = lamp.color
	light.Shadows = true
	light.Parent = head
end

builders.tree = function(model, origin)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Govde", size = Vector3.new(2, 11, 2),
		cf = origin * CFrame.new(0, 5.5, 0), color = C.bark,
		material = Enum.Material.Wood, parent = model,
	})
	Build.pillar({
		name = "Kok", size = Vector3.new(3.2, 1.2, 3.2),
		cf = origin * CFrame.new(0, 0.6, 0), color = C.darkBark,
		material = Enum.Material.Wood, canCollide = false, castShadow = false, parent = model,
	})
	-- Dallar
	for index = 1, 3 do
		local angle = (index / 3) * math.pi * 2
		d("Dal", Vector3.new(0.6, 0.6, 3), Vector3.new(math.cos(angle) * 1.1, 8.5, math.sin(angle) * 1.1),
			C.bark, Enum.Material.Wood, CFrame.Angles(math.rad(35), -angle, 0))
	end
	local crowns = {
		{ Vector3.new(9, 8, 9), Vector3.new(0, 12, 0), C.leaf },
		{ Vector3.new(7, 6.5, 7), Vector3.new(-2.5, 10.5, 1.5), C.darkLeaf },
		{ Vector3.new(6.5, 6, 6.5), Vector3.new(2.6, 10.8, -1.4), C.midLeaf },
		{ Vector3.new(5.5, 5, 5.5), Vector3.new(0.8, 14.2, 1), C.leaf },
		{ Vector3.new(4.8, 4.4, 4.8), Vector3.new(-1.8, 13.4, -1.8), C.midLeaf },
	}
	for index, crown in crowns do
		Build.ball({
			name = "Yaprak" .. index,
			size = crown[1],
			cf = origin * CFrame.new(crown[2]),
			color = crown[3],
			material = Enum.Material.Grass,
			canCollide = false,
			castShadow = index <= 2,
			parent = model,
		})
	end
end

builders.bush = function(model, origin)
	local blobs = {
		{ Vector3.new(4, 3.2, 4), Vector3.new(0, 1.5, 0), C.leaf },
		{ Vector3.new(3, 2.4, 3), Vector3.new(-1.6, 1.1, 0.8), C.darkLeaf },
		{ Vector3.new(3.2, 2.6, 3.2), Vector3.new(1.5, 1.2, -0.7), C.midLeaf },
		{ Vector3.new(2.4, 2, 2.4), Vector3.new(0.6, 2.4, 0.9), C.leaf },
		{ Vector3.new(2, 1.7, 2), Vector3.new(-1, 2.2, -1), C.midLeaf },
		{ Vector3.new(1.8, 1.5, 1.8), Vector3.new(1.7, 2.5, 0.4), C.darkLeaf },
		{ Vector3.new(1.6, 1.4, 1.6), Vector3.new(-0.4, 3.1, 0.2), C.leaf },
	}
	for index, blob in blobs do
		Build.ball({
			name = "Cali" .. index,
			size = blob[1],
			cf = origin * CFrame.new(blob[2]),
			color = blob[3],
			material = Enum.Material.Grass,
			canCollide = index == 1,
			castShadow = index <= 2,
			parent = model,
		})
	end
	-- Toprak tumsegi ve birkac cicek: caliyi tek renkli bir yigin
	-- olmaktan cikariyor.
	Build.ball({
		name = "Toprak", size = Vector3.new(4.4, 1, 4.4),
		cf = origin * CFrame.new(0, 0.2, 0), color = C.soil,
		material = Enum.Material.Ground, canCollide = false, castShadow = false, parent = model,
	})
	for index, spot in { Vector3.new(1.2, 2.9, -1.1), Vector3.new(-1.5, 2.6, 0.9), Vector3.new(0.3, 3.4, -0.6) } do
		Build.ball({
			name = "Cicek" .. index,
			size = Vector3.new(0.5, 0.5, 0.5),
			cf = origin * CFrame.new(spot),
			color = if index % 2 == 0 then C.accent else C.towel,
			canCollide = false, castShadow = false, parent = model,
		})
	end
end

builders.plant = function(model, origin)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Saksi", size = Vector3.new(2.3, 2, 2.3),
		cf = origin * CFrame.new(0, 1, 0), color = C.terracotta,
		material = Enum.Material.Slate, parent = model,
	})
	Build.pillar({
		name = "SaksiAgzi", size = Vector3.new(2.6, 0.32, 2.6),
		cf = origin * CFrame.new(0, 2.05, 0), color = C.terracotta,
		material = Enum.Material.Slate, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "SaksiTabani", size = Vector3.new(1.9, 0.22, 1.9),
		cf = origin * CFrame.new(0, 0.11, 0), color = C.soil,
		material = Enum.Material.Slate, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Toprak", size = Vector3.new(2.1, 0.25, 2.1),
		cf = origin * CFrame.new(0, 2, 0), color = C.soil,
		material = Enum.Material.Ground, canCollide = false, castShadow = false, parent = model,
	})
	-- Sap ve yapraklar
	d("Sap", Vector3.new(0.22, 1.6, 0.22), Vector3.new(0, 2.8, 0), C.darkLeaf, Enum.Material.Grass)
	local leaves = {
		{ Vector3.new(3.2, 3.4, 3.2), Vector3.new(0, 4.1, 0), C.leaf },
		{ Vector3.new(2.3, 2.3, 2.3), Vector3.new(0.9, 5.1, -0.4), C.darkLeaf },
		{ Vector3.new(2, 2, 2), Vector3.new(-1, 4.9, 0.6), C.midLeaf },
		{ Vector3.new(1.6, 1.6, 1.6), Vector3.new(0.2, 5.9, 0.5), C.leaf },
	}
	for index, leaf in leaves do
		Build.ball({
			name = "Yaprak" .. index,
			size = leaf[1],
			cf = origin * CFrame.new(leaf[2]),
			color = leaf[3],
			material = Enum.Material.Grass,
			canCollide = false,
			castShadow = index == 1,
			parent = model,
		})
	end
end

-- ── Dekorasyon esyalari ───────────────────────────────────────────────
-- Bunlar magazadan satin alinip aciliyor (data/decor.json). Kurulusta
-- hepsi INSA EDILIYOR ama gorunmez; boylece hepsi verify_layout'un
-- cakisma denetiminden geciyor ve calisma aninda hicbir yeni parca
-- yaratilmiyor.

builders.aquarium = function(model, origin)
	local d = makerDecor(model, origin)
	-- Dolap
	Build.part({
		name = "Dolap", size = Vector3.new(5.2, 1.9, 1.7),
		cf = origin * CFrame.new(0, 0.95, 0), color = C.darkWood,
		material = Enum.Material.Wood, parent = model,
	})
	d("DolapAlin", Vector3.new(5.4, 0.22, 1.9), Vector3.new(0, 1.95, 0), C.midWood, Enum.Material.Wood)
	for index = 1, 4 do
		Detail.leg(model, origin * CFrame.new(
			if index <= 2 then -2.2 else 2.2,
			0.1,
			if index % 2 == 0 then -0.6 else 0.6
		), 0.2, C.darkSteel)
	end
	-- Cam govde
	local tank = Build.part({
		name = "Cam", size = Vector3.new(5.0, 2.7, 1.5),
		cf = origin * CFrame.new(0, 3.45, 0), color = C.glass,
		material = Enum.Material.Glass, parent = model,
	})
	tank.Transparency = 0.65
	d("Su", Vector3.new(4.8, 2.3, 1.3), Vector3.new(0, 3.3, 0), C.water, Enum.Material.Glass)
	d("Kapak", Vector3.new(5.2, 0.22, 1.7), Vector3.new(0, 4.9, 0), C.darkSteel)
	d("Kum", Vector3.new(4.8, 0.3, 1.3), Vector3.new(0, 2.25, 0), C.sand, Enum.Material.Sand)
	-- Bitkiler ve tasla dolu bir dip
	for index = 1, 4 do
		local x = -1.8 + (index - 1) * 1.2
		d(
			"Yosun" .. index,
			Vector3.new(0.22, 1.1 + (index % 3) * 0.35, 0.22),
			Vector3.new(x, 2.95 + (index % 3) * 0.18, 0),
			if index % 2 == 0 then C.leaf else C.darkLeaf,
			Enum.Material.Grass
		)
	end
	for index = 1, 3 do
		Build.ball({
			name = "Tas" .. index,
			size = Vector3.new(0.55, 0.4, 0.45),
			cf = origin * CFrame.new(-1.2 + index * 1.1, 2.5, 0),
			color = C.darkSteel,
			material = Enum.Material.Slate,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	-- Balıklar: hareket AnimalAnimator'da degil, burada da yok; bunlar
	-- sabit sus. Hareketli olmalari icin bir Motor6D gerekirdi ve bir
	-- sus esyasi ugruna rig kurmak, kare butcesini bosa harcamak olurdu.
	local fish = { C.accent, C.teal, C.towel }
	for index, color in fish do
		Build.ball({
			name = "Balik" .. index,
			size = Vector3.new(0.5, 0.3, 0.24),
			cf = origin * CFrame.new(-1.4 + index * 1.3, 3.4 + (index % 2) * 0.5, 0),
			color = color,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	d("Aydinlatma", Vector3.new(4.6, 0.1, 0.5), Vector3.new(0, 4.72, 0), C.paleTeal)
end

builders.wallClock = function(model, origin)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Kasa", size = Vector3.new(2.7, 0.34, 2.7),
		cf = origin * CFrame.new(0, 7.4, 0) * CFrame.Angles(math.pi / 2, 0, 0),
		color = C.darkWood, material = Enum.Material.Wood,
		canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Kadran", size = Vector3.new(2.3, 0.12, 2.3),
		cf = origin * CFrame.new(0, 7.4, -0.2) * CFrame.Angles(math.pi / 2, 0, 0),
		color = C.white,
		canCollide = false, castShadow = false, parent = model,
	})
	-- Saat isaretleri: 12 kucuk cizgi, cember uzerinde.
	for index = 1, 12 do
		local angle = index / 12 * math.pi * 2
		d(
			"Isaret" .. index,
			Vector3.new(0.1, if index % 3 == 0 then 0.34 else 0.2, 0.06),
			Vector3.new(math.sin(angle) * 0.92, 7.4 + math.cos(angle) * 0.92, -0.3),
			C.navy,
			nil,
			CFrame.Angles(0, 0, -angle)
		)
	end
	d("Akrep", Vector3.new(0.12, 0.62, 0.06), Vector3.new(0.16, 7.68, -0.32), C.navy, nil, CFrame.Angles(0, 0, math.rad(-25)))
	d("Yelkovan", Vector3.new(0.09, 0.92, 0.06), Vector3.new(-0.28, 7.28, -0.32), C.navy, nil, CFrame.Angles(0, 0, math.rad(70)))
	d("Merkez", Vector3.new(0.18, 0.18, 0.08), Vector3.new(0, 7.4, -0.34), C.accent)
end

builders.certificate = function(model, origin)
	local d = makerDecor(model, origin)
	Detail.plate(
		model,
		origin * CFrame.new(0, 7.1, -0.16),
		2.0,
		2.6,
		C.darkWood,
		C.gold,
		Loc.get("decor.certificate.text")
	)
	d("Cerceve", Vector3.new(2.4, 3.0, 0.16), Vector3.new(0, 7.1, 0), C.darkWood, Enum.Material.Wood)
	d("Pasepartu", Vector3.new(2.0, 2.6, 0.06), Vector3.new(0, 7.1, -0.1), C.white)
	d("Muhur", Vector3.new(0.42, 0.42, 0.06), Vector3.new(0.6, 6.2, -0.2), C.accent)
	d("Kurdele1", Vector3.new(0.12, 0.5, 0.05), Vector3.new(0.52, 5.9, -0.2), C.teal, nil, CFrame.Angles(0, 0, math.rad(12)))
	d("Kurdele2", Vector3.new(0.12, 0.5, 0.05), Vector3.new(0.68, 5.9, -0.2), C.teal, nil, CFrame.Angles(0, 0, math.rad(-12)))
	d("Cizgi1", Vector3.new(1.3, 0.07, 0.05), Vector3.new(0, 7.3, -0.2), C.shadowWhite)
	d("Cizgi2", Vector3.new(1.0, 0.07, 0.05), Vector3.new(0, 7.05, -0.2), C.shadowWhite)
	d("Cizgi3", Vector3.new(1.15, 0.07, 0.05), Vector3.new(0, 6.8, -0.2), C.shadowWhite)
end

builders.flowerPot = function(model, origin)
	local d = makerDecor(model, origin)
	Build.pillar({
		name = "Saksi", size = Vector3.new(2.4, 1.9, 2.4),
		cf = origin * CFrame.new(0, 0.95, 0), color = C.terracotta,
		material = Enum.Material.Slate, parent = model,
	})
	Build.pillar({
		name = "SaksiAgzi", size = Vector3.new(2.6, 0.3, 2.6),
		cf = origin * CFrame.new(0, 1.95, 0), color = C.terracotta,
		material = Enum.Material.Slate, canCollide = false, castShadow = false, parent = model,
	})
	Build.pillar({
		name = "Toprak", size = Vector3.new(2.2, 0.22, 2.2),
		cf = origin * CFrame.new(0, 1.9, 0), color = C.soil,
		material = Enum.Material.Ground, canCollide = false, castShadow = false, parent = model,
	})
	-- Cicek sapları ve tomurcuklar: saksi bitkisinden farki, renkli
	-- ciceklerin olmasi - klinigi "yasayan" gosteren sey bu.
	local blooms = {
		{ Vector3.new(0.7, 1.6, 0), C.accent },
		{ Vector3.new(-0.62, 1.35, 0.4), C.towel },
		{ Vector3.new(0.1, 1.8, -0.55), C.teal },
		{ Vector3.new(-0.2, 1.5, -0.15), C.blossom },
	}
	for index, bloom in blooms do
		local offset = bloom[1] :: Vector3
		d(
			"Sap" .. index,
			Vector3.new(0.14, offset.Y, 0.14),
			Vector3.new(offset.X, 2.0 + offset.Y * 0.5, offset.Z),
			C.darkLeaf,
			Enum.Material.Grass
		)
		Build.ball({
			name = "Cicek" .. index,
			size = Vector3.new(0.72, 0.62, 0.72),
			cf = origin * CFrame.new(offset.X, 2.05 + offset.Y, offset.Z),
			color = bloom[2] :: Color3,
			canCollide = false, castShadow = false, parent = model,
		})
	end
	for index = 1, 3 do
		Build.ball({
			name = "Yaprak" .. index,
			size = Vector3.new(1.05, 0.7, 1.05),
			cf = origin * CFrame.new(-0.7 + index * 0.55, 2.35, 0.45),
			color = if index % 2 == 0 then C.leaf else C.midLeaf,
			material = Enum.Material.Grass,
			canCollide = false, castShadow = false, parent = model,
		})
	end
end

--- Bir esyayi kurar. Bilinmeyen id sessizce yutulmaz.
function Props.build(id: string, origin: CFrame, parent: Instance): Model?
	local builder = builders[id]
	if builder == nil then
		warn(("[Props] bilinmeyen esya: '%s' (data/clinic.json)"):format(id))
		return nil
	end

	local model = Build.model(id, parent)
	builder(model, origin)

	-- AssetIds kacamagi: kullanici bir mesh ID yazdiysa prosedurel govdeyi
	-- gizleyip mesh'i koyuyoruz. Yuva bossa (varsayilan) hicbir sey olmuyor.
	local meshId = Assets.mesh(id)
	if meshId ~= nil then
		for _, descendant in model:GetDescendants() do
			if descendant:IsA("BasePart") then
				descendant.Transparency = 1
				descendant.CanCollide = false
			end
		end
		local swap = Instance.new("MeshPart")
		swap.Name = "CreatorStoreMesh"
		swap.Anchored = true
		swap.CFrame = origin
		swap.Parent = model
		local ok = pcall(function()
			(swap :: any).MeshId = meshId
		end)
		if not ok then
			warn(("[Props] '%s' icin mesh yuklenemedi, prosedurel modele donuldu"):format(id))
			swap:Destroy()
			for _, descendant in model:GetDescendants() do
				if descendant:IsA("BasePart") then
					descendant.Transparency = 0
					descendant.CanCollide = true
				end
			end
		end
	end

	local primary = model:FindFirstChildWhichIsA("BasePart")
	if primary ~= nil then
		model.PrimaryPart = primary
	end
	return model
end

--- clinic.json'daki her esya id'sinin karsiligi var mi? SelfTest kullaniyor.
function Props.known(id: string): boolean
	return builders[id] ~= nil
end

--- Bilinen butun esya kimlikleri (SelfTest olcum yapiyor).
function Props.allIds(): { string }
	local ids = {}
	for id in builders do
		table.insert(ids, id)
	end
	table.sort(ids)
	return ids
end

return Props
