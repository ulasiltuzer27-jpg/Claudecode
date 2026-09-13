--!strict
-- Detail.luau
-- ===========
-- Tekrar eden detay parcalari. 26 esyanin hepsi ayni kalite dilinden
-- konussun diye tek yerde toplandi.
--
-- Neden ayri modul?
-- -----------------
-- "Modelleri daha iyi yap" demek pratikte "her esyaya kenar pahi, kulp,
-- ayak, kablo, panel ekle" demek. Bunu 26 esyada tek tek yazmak hem
-- 26 kez tekrar hem de 26 farkli kalite seviyesi olurdu. Burada bir kez
-- yaziliyor, esyalar cagiriyor.
--
-- Performans kurali
-- -----------------
-- Buradaki parcalarin HEPSI `CanCollide = false` ve `CastShadow = false`.
-- Detay parcasi carpismaya da golgeye de katilmamali: govdeler zaten
-- ikisini de yapiyor, detaylar yalnizca goze hitap ediyor. Bu kural
-- olmadan detay eklemek kareyi dusururdu.

local Build = require(script.Parent.Build)

local Detail = {}

local function decor(spec: Build.PartSpec): Part
	spec.canCollide = false
	spec.castShadow = false
	return Build.part(spec)
end

--- Bir kutunun ust kenarlarina ince pah serit. Duz bloklarin en cok ele
--- veren yani keskin kenarlari; bu serit onu kiriyor.
function Detail.trim(parent: Instance, cf: CFrame, size: Vector3, thickness: number, color: Color3)
	local half = size * 0.5
	local edges = {
		{ Vector3.new(size.X + thickness, thickness, thickness), Vector3.new(0, half.Y, half.Z) },
		{ Vector3.new(size.X + thickness, thickness, thickness), Vector3.new(0, half.Y, -half.Z) },
		{ Vector3.new(thickness, thickness, size.Z + thickness), Vector3.new(half.X, half.Y, 0) },
		{ Vector3.new(thickness, thickness, size.Z + thickness), Vector3.new(-half.X, half.Y, 0) },
	}
	for _, edge in edges do
		decor({
			name = "Pah",
			size = edge[1],
			cf = cf * CFrame.new(edge[2]),
			color = color,
			material = Enum.Material.SmoothPlastic,
			parent = parent,
		})
	end
end

--- Dolap kapagi: govdeye gomulu panel + cerceve + kulp.
function Detail.panel(
	parent: Instance,
	cf: CFrame,
	width: number,
	height: number,
	face: Color3,
	inset: Color3,
	handleColor: Color3
)
	decor({
		name = "Kapak",
		size = Vector3.new(width, height, 0.16),
		cf = cf,
		color = face,
		parent = parent,
	})
	decor({
		name = "KapakIci",
		size = Vector3.new(width - 0.6, height - 0.6, 0.1),
		cf = cf * CFrame.new(0, 0, 0.06),
		color = inset,
		parent = parent,
	})
	Detail.handle(parent, cf * CFrame.new(width * 0.32, 0, 0.16), height * 0.42, handleColor)
end

--- Cekmece on yuzu: yatay panel + boydan boya kulp.
function Detail.drawer(parent: Instance, cf: CFrame, width: number, height: number, face: Color3, handleColor: Color3)
	decor({
		name = "Cekmece",
		size = Vector3.new(width, height, 0.16),
		cf = cf,
		color = face,
		parent = parent,
	})
	decor({
		name = "CekmeceKulpu",
		size = Vector3.new(width * 0.5, 0.16, 0.18),
		cf = cf * CFrame.new(0, 0, 0.16),
		color = handleColor,
		material = Enum.Material.Metal,
		parent = parent,
	})
end

--- Cubuk kulp: iki ayak + arasinda bar. Duz bir kutuya yapistirilmis
--- tek parcadan cok daha "gercek" duruyor.
function Detail.handle(parent: Instance, cf: CFrame, length: number, color: Color3)
	for _, side in { -1, 1 } do
		decor({
			name = "KulpAyagi",
			size = Vector3.new(0.12, 0.12, 0.22),
			cf = cf * CFrame.new(0, side * length * 0.5, 0.08),
			color = color,
			material = Enum.Material.Metal,
			parent = parent,
		})
	end
	decor({
		name = "Kulp",
		size = Vector3.new(0.13, length, 0.13),
		cf = cf * CFrame.new(0, 0, 0.19),
		color = color,
		material = Enum.Material.Metal,
		parent = parent,
	})
end

--- Konik mobilya ayagi: govde + yere degen daha ince taban.
function Detail.leg(parent: Instance, top: CFrame, height: number, thickness: number, color: Color3)
	Build.pillar({
		name = "Ayak",
		size = Vector3.new(thickness, height, thickness),
		cf = top * CFrame.new(0, -height * 0.5, 0),
		color = color,
		material = Enum.Material.Metal,
		canCollide = false,
		castShadow = false,
		parent = parent,
	})
	Build.pillar({
		name = "AyakTabani",
		size = Vector3.new(thickness * 1.4, 0.18, thickness * 1.4),
		cf = top * CFrame.new(0, -height + 0.09, 0),
		color = color,
		material = Enum.Material.Metal,
		canCollide = false,
		castShadow = false,
		parent = parent,
	})
end

--- Civata/dugme: kucuk silindir. Metal yuzeylere agirlik veriyor.
function Detail.bolt(parent: Instance, cf: CFrame, diameter: number, color: Color3)
	Build.pillar({
		name = "Civata",
		size = Vector3.new(diameter, 0.1, diameter),
		cf = cf * CFrame.Angles(math.rad(90), 0, 0),
		color = color,
		material = Enum.Material.Metal,
		canCollide = false,
		castShadow = false,
		parent = parent,
	})
end

--- Sarkan kablo: parcali bir egri. Cihazlari duvara/yere baglayan kablo,
--- bir odayi "kurulmus" gosteren en ucuz detay.
function Detail.cable(parent: Instance, from: Vector3, to: Vector3, thickness: number, color: Color3)
	local segments = 6
	local sag = (to - from).Magnitude * 0.18
	local previous = from
	for index = 1, segments do
		local ratio = index / segments
		-- Basit parabol: uclarda sifir, ortada en derin.
		local drop = sag * math.sin(ratio * math.pi)
		local point = from:Lerp(to, ratio) - Vector3.new(0, drop, 0)
		local middle = (previous + point) * 0.5
		local length = (point - previous).Magnitude
		decor({
			name = "Kablo",
			size = Vector3.new(thickness, thickness, length),
			cf = CFrame.lookAt(middle, point),
			color = color,
			parent = parent,
		})
		previous = point
	end
end

--- Bir kutunun cevresine kenarlik (kuvet agzi, tepsi kenari).
function Detail.rim(parent: Instance, cf: CFrame, size: Vector3, thickness: number, color: Color3)
	local half = size * 0.5
	local sides = {
		{ Vector3.new(size.X, thickness, thickness * 1.6), Vector3.new(0, 0, half.Z) },
		{ Vector3.new(size.X, thickness, thickness * 1.6), Vector3.new(0, 0, -half.Z) },
		{ Vector3.new(thickness * 1.6, thickness, size.Z), Vector3.new(half.X, 0, 0) },
		{ Vector3.new(thickness * 1.6, thickness, size.Z), Vector3.new(-half.X, 0, 0) },
	}
	for _, side in sides do
		decor({
			name = "Kenarlik",
			size = side[1],
			cf = cf * CFrame.new(side[2]),
			color = color,
			material = Enum.Material.Metal,
			parent = parent,
		})
	end
end

--- Cerceveli ekran. PARLAMAZ: gercek bir ekran karanlik odada bile
--- etrafini aydinlatmaz, o yuzden Neon degil SmoothPlastic.
function Detail.screen(
	parent: Instance,
	cf: CFrame,
	width: number,
	height: number,
	frameColor: Color3,
	screenColor: Color3,
	text: string?
): BasePart
	decor({
		name = "EkranCercevesi",
		size = Vector3.new(width, height, 0.22),
		cf = cf,
		color = frameColor,
		parent = parent,
	})
	local glass = decor({
		name = "Ekran",
		size = Vector3.new(width - 0.35, height - 0.35, 0.08),
		cf = cf * CFrame.new(0, 0, 0.13),
		color = screenColor,
		material = Enum.Material.SmoothPlastic,
		parent = parent,
	})
	if text ~= nil then
		Build.surfaceText(glass, Enum.NormalId.Front, text, Color3.fromRGB(28, 46, 62))
	end
	return glass
end

--- Dama deseni zemin kaplamasi. Tek renkli genis bir doseme en cok
--- "bitmemis" duran sey; iki tonlu kareler odayi bitiriyor.
function Detail.tiles(
	parent: Instance,
	minXZ: { number },
	maxXZ: { number },
	y: number,
	tileSize: number,
	colorA: Color3,
	colorB: Color3,
	material: Enum.Material
)
	local columns = math.max(1, math.floor((maxXZ[1] - minXZ[1]) / tileSize))
	local rows = math.max(1, math.floor((maxXZ[2] - minXZ[2]) / tileSize))
	local width = (maxXZ[1] - minXZ[1]) / columns
	local depth = (maxXZ[2] - minXZ[2]) / rows

	for column = 1, columns do
		for row = 1, rows do
			-- Yalnizca "koyu" kareler ciziliyor; acik kareler zaten
			-- alttaki dosemenin kendisi. Yari parca, ayni gorunum.
			if (column + row) % 2 ~= 0 then
				continue
			end
			decor({
				name = "Karo",
				size = Vector3.new(width, 0.06, depth),
				cf = CFrame.new(
					minXZ[1] + (column - 0.5) * width,
					y,
					minXZ[2] + (row - 0.5) * depth
				),
				color = colorB,
				material = material,
				parent = parent,
			})
		end
	end
	local _ = colorA -- acik kare = dosemenin kendisi
end

--- Suslu ayakli tabela plakasi (oda tabelalari, kafes etiketleri).
function Detail.plate(
	parent: Instance,
	cf: CFrame,
	width: number,
	height: number,
	frameColor: Color3,
	faceColor: Color3,
	text: string
): BasePart
	decor({
		name = "PlakaCercevesi",
		size = Vector3.new(width + 0.3, height + 0.3, 0.16),
		cf = cf,
		color = frameColor,
		parent = parent,
	})
	local face = decor({
		name = "Plaka",
		size = Vector3.new(width, height, 0.2),
		cf = cf * CFrame.new(0, 0, 0.04),
		color = faceColor,
		parent = parent,
	})
	Build.surfaceText(face, Enum.NormalId.Front, text, frameColor)
	return face
end

return Detail
