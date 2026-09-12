--!strict
-- Props.luau
-- ==========
-- Klinik esyalarinin kod ile uretimi. Her esya temel parcalardan
-- (Part / WedgePart / silindir / kure) kuruluyor; hicbir dis asset'e
-- bagimli degil, bu yuzden oyun ilk acilista eksiksiz gorunuyor.
--
-- Renkler neden burada, olculer neden veride?
-- -------------------------------------------
-- Oyunun DENGESINI etkileyen her sey veride (data/*.json): oda planı,
-- esyanin nerede duracagi, ucretler, sureler. Saf dekorasyon rengi
-- oynanisi degistirmiyor; onu veriye tasimak 26 esya x 5 renk = 130
-- satirlik bir tabloya mal olur ve kimse duzenlemez. Sinir burada
-- cizildi.
--
-- AssetIds kacamagi
-- -----------------
-- Kullanici data/assetIds.json'a bir mesh ID'si yazarsa Assets.mesh()
-- onu dondurur ve prosedurel govdenin yerine o mesh kullanilir. Tablo
-- bos oldugunda (varsayilan) hicbir sey degismez.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Assets = require(Shared.Assets)
local Build = require(Shared.Build)
local Loc = require(Shared.Loc)

local Props = {}

local C = {
	steel = Color3.fromRGB(186, 192, 198),
	darkSteel = Color3.fromRGB(126, 132, 140),
	white = Color3.fromRGB(244, 247, 250),
	offWhite = Color3.fromRGB(226, 230, 234),
	wood = Color3.fromRGB(176, 132, 86),
	darkWood = Color3.fromRGB(126, 92, 58),
	teal = Color3.fromRGB(72, 166, 168),
	deepTeal = Color3.fromRGB(44, 120, 126),
	navy = Color3.fromRGB(48, 62, 84),
	screen = Color3.fromRGB(142, 218, 236),
	pad = Color3.fromRGB(96, 148, 186),
	leaf = Color3.fromRGB(84, 148, 76),
	darkLeaf = Color3.fromRGB(60, 116, 56),
	bark = Color3.fromRGB(104, 78, 54),
	soil = Color3.fromRGB(96, 74, 58),
	glass = Color3.fromRGB(200, 226, 236),
	accent = Color3.fromRGB(232, 106, 96),
	water = Color3.fromRGB(120, 186, 222),
	towel = Color3.fromRGB(238, 226, 206),
	black = Color3.fromRGB(42, 46, 52),
}

type Builder = (model: Model, origin: CFrame) -> ()

-- Her builder'in icinde tekrar eden kalibi kisaltan yardimci uretir.
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

local builders: { [string]: Builder } = {}

-- ── Resepsiyon ve bekleme ─────────────────────────────────────────────

builders.receptionDesk = function(model, origin)
	local p = maker(model, origin)
	p("Govde", Vector3.new(10, 3.2, 2.4), Vector3.new(0, 1.6, 0), C.wood, Enum.Material.Wood)
	p("Tezgah", Vector3.new(11, 0.4, 3.4), Vector3.new(0, 3.4, 0.3), C.offWhite)
	p("On", Vector3.new(11, 1.2, 0.3), Vector3.new(0, 2.9, -1.35), C.teal)
	p("Monitor", Vector3.new(2.6, 1.6, 0.15), Vector3.new(-3, 4.5, 0.6), C.black,
		nil, CFrame.Angles(math.rad(-12), 0, 0))
	p("Ekran", Vector3.new(2.3, 1.3, 0.08), Vector3.new(-3, 4.5, 0.68), C.screen,
		Enum.Material.Neon, CFrame.Angles(math.rad(-12), 0, 0))
	p("Kagitlar", Vector3.new(1.6, 0.1, 1.1), Vector3.new(2.4, 3.65, 0.4), C.white)
end

builders.waitingBench = function(model, origin)
	local p = maker(model, origin)
	p("Oturak", Vector3.new(7, 0.4, 2.2), Vector3.new(0, 1.7, 0), C.teal)
	p("Sirtlik", Vector3.new(7, 2, 0.35), Vector3.new(0, 2.8, -1), C.deepTeal)
	p("Ayak", Vector3.new(0.35, 1.7, 1.8), Vector3.new(-3, 0.85, 0), C.darkSteel, Enum.Material.Metal)
	p("Ayak", Vector3.new(0.35, 1.7, 1.8), Vector3.new(3, 0.85, 0), C.darkSteel, Enum.Material.Metal)
end

builders.outdoorBench = function(model, origin)
	local p = maker(model, origin)
	p("Oturak", Vector3.new(6, 0.35, 2), Vector3.new(0, 1.6, 0), C.wood, Enum.Material.WoodPlanks)
	p("Sirtlik", Vector3.new(6, 1.6, 0.3), Vector3.new(0, 2.5, -0.9), C.wood, Enum.Material.WoodPlanks)
	p("Ayak", Vector3.new(0.4, 1.6, 1.8), Vector3.new(-2.5, 0.8, 0), C.darkWood, Enum.Material.Wood)
	p("Ayak", Vector3.new(0.4, 1.6, 1.8), Vector3.new(2.5, 0.8, 0), C.darkWood, Enum.Material.Wood)
end

builders.waterCooler = function(model, origin)
	local p = maker(model, origin)
	p("Govde", Vector3.new(1.6, 3.4, 1.6), Vector3.new(0, 1.7, 0), C.white)
	p("Musluk", Vector3.new(0.4, 0.5, 0.5), Vector3.new(0, 2.6, 0.9), C.darkSteel, Enum.Material.Metal)
	Build.pillar({
		name = "Damacana",
		size = Vector3.new(1.5, 2.2, 1.5),
		cf = origin * CFrame.new(0, 4.5, 0),
		color = C.water,
		material = Enum.Material.Glass,
		transparency = 0.35,
		parent = model,
	})
end

builders.trashBin = function(model, origin)
	Build.pillar({
		name = "Kova",
		size = Vector3.new(1.5, 2.2, 1.5),
		cf = origin * CFrame.new(0, 1.1, 0),
		color = C.darkSteel,
		material = Enum.Material.Metal,
		parent = model,
	})
	Build.pillar({
		name = "Kapak",
		size = Vector3.new(1.65, 0.25, 1.65),
		cf = origin * CFrame.new(0, 2.3, 0),
		color = C.steel,
		material = Enum.Material.Metal,
		parent = model,
	})
end

builders.poster = function(model, origin)
	local p = maker(model, origin)
	local board = p("Afis", Vector3.new(5, 3.4, 0.15), Vector3.new(0, 7, 0), C.white)
	p("Cerceve", Vector3.new(5.3, 3.7, 0.1), Vector3.new(0, 7, -0.08), C.teal)
	Build.surfaceText(board, Enum.NormalId.Front, Loc.get("prop.poster"), C.navy)
end

-- ── Eczane ve depolama ────────────────────────────────────────────────

builders.medicineShelf = function(model, origin)
	local p = maker(model, origin)
	p("Kasa", Vector3.new(6.5, 8, 1.6), Vector3.new(0, 4, 0), C.offWhite)
	local bottleColors = { C.accent, C.teal, C.water, C.leaf, C.navy }
	for shelf = 1, 4 do
		local y = 1.2 + (shelf - 1) * 1.9
		p("Raf", Vector3.new(6.2, 0.2, 1.5), Vector3.new(0, y, 0.05), C.white)
		for slot = 1, 6 do
			local x = -2.6 + (slot - 1) * 1.05
			Build.pillar({
				name = "Sise",
				size = Vector3.new(0.55, 1.1, 0.55),
				cf = origin * CFrame.new(x, y + 0.65, 0.1),
				color = bottleColors[((shelf + slot) % #bottleColors) + 1],
				material = Enum.Material.Glass,
				transparency = 0.2,
				canCollide = false,
				parent = model,
			})
		end
	end
end

builders.cabinet = function(model, origin)
	local p = maker(model, origin)
	p("Govde", Vector3.new(4.5, 6, 1.8), Vector3.new(0, 3, 0), C.white)
	p("KapakSol", Vector3.new(2.1, 5.4, 0.15), Vector3.new(-1.1, 3, 0.95), C.offWhite)
	p("KapakSag", Vector3.new(2.1, 5.4, 0.15), Vector3.new(1.1, 3, 0.95), C.offWhite)
	p("Kol", Vector3.new(0.15, 1, 0.15), Vector3.new(-0.2, 3, 1.05), C.darkSteel, Enum.Material.Metal)
	p("Kol", Vector3.new(0.15, 1, 0.15), Vector3.new(0.2, 3, 1.05), C.darkSteel, Enum.Material.Metal)
	p("Serit", Vector3.new(4.5, 0.25, 1.85), Vector3.new(0, 5.9, 0), C.teal)
end

builders.towelShelf = function(model, origin)
	local p = maker(model, origin)
	p("Kasa", Vector3.new(4, 5, 1.4), Vector3.new(0, 2.5, 0), C.offWhite)
	for shelf = 1, 3 do
		local y = 1 + (shelf - 1) * 1.6
		p("Raf", Vector3.new(3.8, 0.16, 1.3), Vector3.new(0, y, 0), C.white)
		p("Havlu", Vector3.new(1.5, 0.7, 1.1), Vector3.new(-0.9, y + 0.45, 0), C.towel, Enum.Material.Fabric)
		p("Havlu", Vector3.new(1.5, 0.7, 1.1), Vector3.new(0.9, y + 0.45, 0), C.glass, Enum.Material.Fabric)
	end
end

builders.dryerRack = function(model, origin)
	local p = maker(model, origin)
	p("Dikme", Vector3.new(0.3, 5, 0.3), Vector3.new(-2.5, 2.5, 0), C.darkSteel, Enum.Material.Metal)
	p("Dikme", Vector3.new(0.3, 5, 0.3), Vector3.new(2.5, 2.5, 0), C.darkSteel, Enum.Material.Metal)
	p("Ray", Vector3.new(5.3, 0.22, 0.22), Vector3.new(0, 4.8, 0), C.steel, Enum.Material.Metal)
	p("Ray", Vector3.new(5.3, 0.22, 0.22), Vector3.new(0, 3.4, 0), C.steel, Enum.Material.Metal)
	p("AsiliHavlu", Vector3.new(1.6, 2.2, 0.12), Vector3.new(-1.2, 3.7, 0), C.towel, Enum.Material.Fabric)
	p("AsiliHavlu", Vector3.new(1.6, 2.6, 0.12), Vector3.new(1.2, 3.5, 0), C.water, Enum.Material.Fabric)
end

-- ── Muayene ───────────────────────────────────────────────────────────

builders.examTable = function(model, origin)
	local p = maker(model, origin)
	p("Ayak", Vector3.new(1.4, 2.6, 1.4), Vector3.new(0, 1.3, 0), C.darkSteel, Enum.Material.Metal)
	p("Taban", Vector3.new(3.4, 0.35, 2.6), Vector3.new(0, 0.18, 0), C.darkSteel, Enum.Material.Metal)
	p("Tabla", Vector3.new(7.5, 0.5, 4.2), Vector3.new(0, 2.85, 0), C.steel, Enum.Material.Metal)
	p("Minder", Vector3.new(7.2, 0.5, 4), Vector3.new(0, 3.3, 0), C.pad, Enum.Material.Fabric)
	p("Kenarlik", Vector3.new(7.5, 0.3, 0.25), Vector3.new(0, 3.6, 2.1), C.steel, Enum.Material.Metal)
	p("Kenarlik", Vector3.new(7.5, 0.3, 0.25), Vector3.new(0, 3.6, -2.1), C.steel, Enum.Material.Metal)
	p("Tepsi", Vector3.new(2.4, 0.2, 1.6), Vector3.new(4.8, 3, 0), C.steel, Enum.Material.Metal)
	p("TepsiAyak", Vector3.new(0.25, 3, 0.25), Vector3.new(4.8, 1.5, 0), C.darkSteel, Enum.Material.Metal)
end

builders.surgeryTable = function(model, origin)
	local p = maker(model, origin)
	p("Ayak", Vector3.new(1.8, 2.8, 1.8), Vector3.new(0, 1.4, 0), C.darkSteel, Enum.Material.Metal)
	p("Taban", Vector3.new(4, 0.4, 3), Vector3.new(0, 0.2, 0), C.darkSteel, Enum.Material.Metal)
	p("Tabla", Vector3.new(8.5, 0.55, 4.8), Vector3.new(0, 3.05, 0), C.steel, Enum.Material.Metal)
	p("Ortu", Vector3.new(8.2, 0.35, 4.6), Vector3.new(0, 3.5, 0), C.deepTeal, Enum.Material.Fabric)
	p("Ray", Vector3.new(8.5, 0.25, 0.25), Vector3.new(0, 3.9, 2.4), C.steel, Enum.Material.Metal)
	p("Ray", Vector3.new(8.5, 0.25, 0.25), Vector3.new(0, 3.9, -2.4), C.steel, Enum.Material.Metal)
	p("AletTepsisi", Vector3.new(3, 0.2, 1.8), Vector3.new(5.6, 3.4, 0), C.steel, Enum.Material.Metal)
	p("TepsiAyak", Vector3.new(0.3, 3.4, 0.3), Vector3.new(5.6, 1.7, 0), C.darkSteel, Enum.Material.Metal)
	for i = 1, 4 do
		p("Alet", Vector3.new(0.16, 0.1, 1.2), Vector3.new(4.7 + (i - 1) * 0.55, 3.55, 0), C.steel, Enum.Material.Metal)
	end
end

builders.sink = function(model, origin)
	local p = maker(model, origin)
	p("Tezgah", Vector3.new(4.5, 0.4, 2.4), Vector3.new(0, 3, 0), C.offWhite)
	p("Dolap", Vector3.new(4.3, 2.8, 2.2), Vector3.new(0, 1.4, 0), C.white)
	p("Lavabo", Vector3.new(2.4, 0.5, 1.6), Vector3.new(0, 2.85, 0.1), C.steel, Enum.Material.Metal)
	p("Musluk", Vector3.new(0.2, 1.4, 0.2), Vector3.new(0, 3.9, -0.8), C.steel, Enum.Material.Metal)
	p("MuslukUcu", Vector3.new(0.2, 0.2, 1), Vector3.new(0, 4.5, -0.35), C.steel, Enum.Material.Metal)
	p("SabunKabi", Vector3.new(0.5, 0.9, 0.5), Vector3.new(1.7, 3.65, 0), C.teal)
end

builders.monitor = function(model, origin)
	local p = maker(model, origin)
	p("Ayak", Vector3.new(1.6, 0.25, 1.2), Vector3.new(0, 0.12, 0), C.black)
	p("Direk", Vector3.new(0.3, 4, 0.3), Vector3.new(0, 2, 0), C.darkSteel, Enum.Material.Metal)
	local screen = p("Kasa", Vector3.new(4.4, 2.8, 0.3), Vector3.new(0, 4.6, 0), C.black,
		nil, CFrame.Angles(math.rad(-8), 0, 0))
	p("Ekran", Vector3.new(4.1, 2.5, 0.12), Vector3.new(0, 4.6, 0.2), C.screen,
		Enum.Material.Neon, CFrame.Angles(math.rad(-8), 0, 0))
	Build.surfaceText(screen, Enum.NormalId.Front, Loc.get("prop.monitor"), C.navy)
end

builders.ivStand = function(model, origin)
	local p = maker(model, origin)
	p("Taban", Vector3.new(2, 0.25, 2), Vector3.new(0, 0.12, 0), C.darkSteel, Enum.Material.Metal)
	Build.pillar({
		name = "Direk",
		size = Vector3.new(0.25, 7, 0.25),
		cf = origin * CFrame.new(0, 3.5, 0),
		color = C.steel,
		material = Enum.Material.Metal,
		parent = model,
	})
	p("Kanca", Vector3.new(1.2, 0.16, 0.16), Vector3.new(0.4, 6.9, 0), C.steel, Enum.Material.Metal)
	p("Serum", Vector3.new(1, 1.8, 0.5), Vector3.new(0.8, 6, 0), C.water, Enum.Material.Glass)
	p("Hortum", Vector3.new(0.1, 3, 0.1), Vector3.new(0.8, 3.6, 0), C.glass)
end

builders.scale = function(model, origin)
	local p = maker(model, origin)
	p("Platform", Vector3.new(4, 0.5, 3), Vector3.new(0, 0.35, 0), C.steel, Enum.Material.Metal)
	p("Kaymaz", Vector3.new(3.7, 0.12, 2.7), Vector3.new(0, 0.65, 0), C.black, Enum.Material.Fabric)
	p("Direk", Vector3.new(0.25, 3, 0.25), Vector3.new(0, 1.9, -1.3), C.darkSteel, Enum.Material.Metal)
	local display = p("Gosterge", Vector3.new(1.8, 1, 0.3), Vector3.new(0, 3.5, -1.3), C.black)
	Build.surfaceText(display, Enum.NormalId.Front, "0.0 kg", C.screen)
end

builders.xrayMachine = function(model, origin)
	local p = maker(model, origin)
	p("Taban", Vector3.new(4.5, 0.6, 4), Vector3.new(0, 0.3, 0), C.darkSteel, Enum.Material.Metal)
	p("Kasa", Vector3.new(3.6, 5.5, 3.2), Vector3.new(0, 3.2, 0), C.offWhite)
	p("Kol", Vector3.new(0.6, 0.6, 5), Vector3.new(0, 6.2, 2.2), C.steel, Enum.Material.Metal)
	p("Kafa", Vector3.new(2.6, 1.6, 2.6), Vector3.new(0, 5.6, 4.4), C.white)
	local lens = p("Mercek", Vector3.new(1.8, 0.2, 1.8), Vector3.new(0, 4.75, 4.4), C.screen, Enum.Material.Neon)
	local glow = Instance.new("PointLight")
	glow.Color = Color3.fromRGB(150, 210, 255)
	glow.Brightness = 1.2
	glow.Range = 14
	glow.Parent = lens
	local panel = p("Panel", Vector3.new(2.6, 1.8, 0.25), Vector3.new(2.4, 4.2, -1.4), C.black,
		nil, CFrame.Angles(0, math.rad(-30), 0))
	Build.surfaceText(panel, Enum.NormalId.Front, Loc.get("prop.xray"), C.screen)
end

builders.surgeryLight = function(model, origin)
	local p = maker(model, origin)
	p("Baglanti", Vector3.new(0.4, 3.2, 0.4), Vector3.new(0, 11.4, 0), C.steel, Enum.Material.Metal)
	Build.pillar({
		name = "Kubbe",
		size = Vector3.new(5, 1, 5),
		cf = origin * CFrame.new(0, 9.6, 0),
		color = C.steel,
		material = Enum.Material.Metal,
		parent = model,
	})
	local lens = Build.pillar({
		name = "Mercek",
		size = Vector3.new(4.4, 0.35, 4.4),
		cf = origin * CFrame.new(0, 9.1, 0),
		color = Color3.fromRGB(255, 252, 240),
		material = Enum.Material.Neon,
		canCollide = false,
		parent = model,
	})
	local spot = Instance.new("SpotLight")
	spot.Face = Enum.NormalId.Bottom
	spot.Angle = 80
	spot.Brightness = 4
	spot.Range = 30
	spot.Color = Color3.fromRGB(255, 250, 236)
	spot.Shadows = true
	spot.Parent = lens
end

-- ── Kogus ve yikama ───────────────────────────────────────────────────

builders.kennelCage = function(model, origin)
	local p = maker(model, origin)
	p("Taban", Vector3.new(6, 0.4, 5), Vector3.new(0, 0.2, 0), C.offWhite)
	p("Yatak", Vector3.new(5, 0.4, 4), Vector3.new(0, 0.55, 0), C.pad, Enum.Material.Fabric)
	p("Tavan", Vector3.new(6, 0.35, 5), Vector3.new(0, 5.2, 0), C.offWhite)
	p("ArkaDuvar", Vector3.new(6, 5, 0.25), Vector3.new(0, 2.7, -2.4), C.white)
	p("YanDuvar", Vector3.new(0.25, 5, 5), Vector3.new(-2.9, 2.7, 0), C.white)
	p("YanDuvar", Vector3.new(0.25, 5, 5), Vector3.new(2.9, 2.7, 0), C.white)
	-- On yuz: dikey parmakliklar
	for i = 0, 8 do
		p("Parmaklik", Vector3.new(0.18, 4.8, 0.18), Vector3.new(-2.6 + i * 0.65, 2.7, 2.4), C.steel, Enum.Material.Metal)
	end
	p("Kilit", Vector3.new(0.6, 0.5, 0.35), Vector3.new(2.2, 2.7, 2.5), C.darkSteel, Enum.Material.Metal)
end

builders.feedStation = function(model, origin)
	local p = maker(model, origin)
	p("Paspas", Vector3.new(5, 0.15, 3), Vector3.new(0, 0.08, 0), C.deepTeal, Enum.Material.Fabric)
	Build.pillar({
		name = "Mama",
		size = Vector3.new(1.8, 0.7, 1.8),
		cf = origin * CFrame.new(-1.2, 0.4, 0),
		color = C.accent,
		parent = model,
	})
	Build.pillar({
		name = "Su",
		size = Vector3.new(1.8, 0.7, 1.8),
		cf = origin * CFrame.new(1.2, 0.4, 0),
		color = C.water,
		parent = model,
	})
end

builders.washTub = function(model, origin)
	local p = maker(model, origin)
	p("Ayak", Vector3.new(5.5, 2.4, 3.5), Vector3.new(0, 1.2, 0), C.offWhite)
	p("Kuvet", Vector3.new(6, 1.6, 4), Vector3.new(0, 3.2, 0), C.white)
	p("Su", Vector3.new(5.4, 0.4, 3.4), Vector3.new(0, 3.7, 0), C.water, Enum.Material.Glass)
	p("Kenar", Vector3.new(6.2, 0.3, 4.2), Vector3.new(0, 4.05, 0), C.glass)
	p("Dus", Vector3.new(0.2, 3.5, 0.2), Vector3.new(0, 5.6, -1.8), C.steel, Enum.Material.Metal)
	p("DusBasi", Vector3.new(1, 0.3, 1), Vector3.new(0, 7.1, -1.2), C.steel, Enum.Material.Metal)
	p("Sampuan", Vector3.new(0.5, 1, 0.5), Vector3.new(2.4, 4.5, 0), C.teal)
end

-- ── Disari ────────────────────────────────────────────────────────────

builders.clinicSign = function(model, origin)
	local p = maker(model, origin)
	p("Direk", Vector3.new(0.5, 12, 0.5), Vector3.new(-7, 6, 0), C.darkSteel, Enum.Material.Metal)
	p("Direk", Vector3.new(0.5, 12, 0.5), Vector3.new(7, 6, 0), C.darkSteel, Enum.Material.Metal)
	local board = p("Tabela", Vector3.new(16, 4, 0.45), Vector3.new(0, 12.5, 0), C.teal)
	p("Cerceve", Vector3.new(16.6, 4.6, 0.3), Vector3.new(0, 12.5, 0.1), C.white)
	Build.surfaceText(board, Enum.NormalId.Back, Loc.get("clinic.name"), C.white)

	-- Haci: gorunur bir veteriner isareti.
	p("HacDikey", Vector3.new(1, 3, 0.2), Vector3.new(-6.2, 12.5, -0.35), C.white, Enum.Material.Neon)
	p("HacYatay", Vector3.new(3, 1, 0.2), Vector3.new(-6.2, 12.5, -0.35), C.white, Enum.Material.Neon)
end

builders.lampPost = function(model, origin)
	Build.pillar({
		name = "Direk",
		size = Vector3.new(0.5, 11, 0.5),
		cf = origin * CFrame.new(0, 5.5, 0),
		color = C.navy,
		material = Enum.Material.Metal,
		parent = model,
	})
	local head = Build.part({
		name = "Fener",
		size = Vector3.new(1.8, 1.4, 1.8),
		cf = origin * CFrame.new(0, 11.4, 0),
		color = Color3.fromRGB(255, 246, 216),
		material = Enum.Material.Neon,
		parent = model,
	})
	local light = Instance.new("PointLight")
	light.Brightness = 2.2
	light.Range = 30
	light.Color = Color3.fromRGB(255, 236, 190)
	light.Shadows = true
	light.Parent = head
end

builders.tree = function(model, origin)
	Build.pillar({
		name = "Govde",
		size = Vector3.new(2, 11, 2),
		cf = origin * CFrame.new(0, 5.5, 0),
		color = C.bark,
		material = Enum.Material.Wood,
		parent = model,
	})
	local crowns = {
		{ Vector3.new(9, 8, 9), Vector3.new(0, 12, 0), C.leaf },
		{ Vector3.new(7, 6.5, 7), Vector3.new(-2.5, 10.5, 1.5), C.darkLeaf },
		{ Vector3.new(6.5, 6, 6.5), Vector3.new(2.6, 10.8, -1.4), C.leaf },
	}
	for index, crown in crowns do
		Build.ball({
			name = "Yaprak" .. index,
			size = crown[1],
			cf = origin * CFrame.new(crown[2]),
			color = crown[3],
			material = Enum.Material.Grass,
			canCollide = false,
			parent = model,
		})
	end
end

builders.bush = function(model, origin)
	local blobs = {
		{ Vector3.new(4, 3.2, 4), Vector3.new(0, 1.5, 0) },
		{ Vector3.new(3, 2.4, 3), Vector3.new(-1.6, 1.1, 0.8) },
		{ Vector3.new(3.2, 2.6, 3.2), Vector3.new(1.5, 1.2, -0.7) },
	}
	for index, blob in blobs do
		Build.ball({
			name = "Cali" .. index,
			size = blob[1],
			cf = origin * CFrame.new(blob[2]),
			color = if index % 2 == 0 then C.darkLeaf else C.leaf,
			material = Enum.Material.Grass,
			parent = model,
		})
	end
end

builders.plant = function(model, origin)
	Build.pillar({
		name = "Saksi",
		size = Vector3.new(2.2, 2, 2.2),
		cf = origin * CFrame.new(0, 1, 0),
		color = C.soil,
		material = Enum.Material.Slate,
		parent = model,
	})
	Build.ball({
		name = "Yaprak",
		size = Vector3.new(3.4, 3.6, 3.4),
		cf = origin * CFrame.new(0, 3.6, 0),
		color = C.leaf,
		material = Enum.Material.Grass,
		canCollide = false,
		parent = model,
	})
	Build.ball({
		name = "Yaprak2",
		size = Vector3.new(2.4, 2.4, 2.4),
		cf = origin * CFrame.new(0.9, 4.9, -0.4),
		color = C.darkLeaf,
		material = Enum.Material.Grass,
		canCollide = false,
		parent = model,
	})
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
		-- MeshId yalnizca MeshPart olusturulduktan sonra atanabiliyor.
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

	-- Esyalar sabit; PrimaryPart oyun modullerinin konum sormasi icin.
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

return Props
