--!strict
-- Walls.luau
-- ==========
-- Kapi ve pencere boslukli duvar kurucusu.
--
-- Neden ayri bir modul?
-- ---------------------
-- Bu kod once `Clinic.luau` icindeydi ve yalnizca klinik planini
-- taniyordu. Lobi de ayni duvarlari (supurgelik, tavan kornisi, kapi
-- cercevesi, pencere denizligi) isteyince iki secenek vardi: ayni
-- mantigi ikinci kez yazmak ya da buraya tasimak. Ikinci kez yazmak,
-- bir duvar hatasini iki yerde birden duzeltmek demekti.
--
-- Modul bir PLAN tablosu aliyor (`Blueprint`) ve o plandan baskasini
-- bilmiyor: klinik de lobi de ayni alanlari bildiriyor, kurucu ikisini
-- ayirt etmiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Build = require(Shared.Build)

local Walls = {}

export type Blueprint = {
	wallHeight: number,
	wallThickness: number,
	doorHeight: number,
	colors: { [string]: { number } },
	levels: {
		skirtHeight: number,
		corniceHeight: number,
		trimBulge: number,
	},
}

local EPSILON = 1e-3

--- Duvarin bir parcasi. Kapi/pencere bosluklari bu parcalarin ARASINDA
--- kaliyor: delik acmak yerine bosluk birakiyoruz, cunku Roblox'ta
--- parcaya delik acilamaz.
function Walls.segment(
	blueprint: Blueprint,
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	axisFrom: number,
	axisTo: number,
	yFrom: number,
	yTo: number,
	color: Color3,
	name: string?,
	thickness: number?
)
	local length = axisTo - axisFrom
	if length <= EPSILON then
		return
	end
	local height = yTo - yFrom
	if height <= EPSILON then
		return
	end

	local center = (axisFrom + axisTo) * 0.5
	local y = (yFrom + yTo) * 0.5
	local depth = thickness or blueprint.wallThickness

	local size, position
	if horizontal then
		size = Vector3.new(length, height, depth)
		position = Vector3.new(center, y, fixed)
	else
		size = Vector3.new(depth, height, length)
		position = Vector3.new(fixed, y, center)
	end

	Build.part({
		name = name or "Duvar",
		size = size,
		cf = CFrame.new(position),
		color = color,
		material = Enum.Material.SmoothPlastic,
		parent = parent,
	})
end

--- Kapi cercevesi: iki dikme + ust kiris. Bos bir delik yerine cerceve
--- koymak binayi bitmis gosteriyor.
local function doorFrame(
	blueprint: Blueprint,
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	at: number,
	width: number,
	color: Color3
)
	local thickness = blueprint.wallThickness + 0.15
	local postWidth = 0.5
	local top = blueprint.doorHeight

	local function piece(axisCenter: number, axisSize: number, y: number, ySize: number)
		local size, position
		if horizontal then
			size = Vector3.new(axisSize, ySize, thickness)
			position = Vector3.new(axisCenter, y, fixed)
		else
			size = Vector3.new(thickness, ySize, axisSize)
			position = Vector3.new(fixed, y, axisCenter)
		end
		Build.part({
			name = "KapiCercevesi",
			size = size,
			cf = CFrame.new(position),
			color = color,
			material = Enum.Material.SmoothPlastic,
			canCollide = false,
			parent = parent,
		})
	end

	piece(at - width * 0.5 - postWidth * 0.5, postWidth, top * 0.5, top)
	piece(at + width * 0.5 + postWidth * 0.5, postWidth, top * 0.5, top)
	piece(at, width + postWidth * 2, top + 0.25, 0.5)
end

--- Cam bosluğu: kapi ile AYNI mantik, farki alttan ve ustten duvarin
--- devam etmesi. Icine cam ve cerceve konuyor.
local function windowGlass(
	blueprint: Blueprint,
	parent: Instance,
	horizontal: boolean,
	fixed: number,
	at: number,
	width: number,
	sill: number,
	height: number,
	frameColor: Color3
)
	local centerY = sill + height * 0.5
	local function piece(
		name: string,
		axisCenter: number,
		axisSize: number,
		y: number,
		ySize: number,
		thickness: number,
		color: Color3,
		material: Enum.Material,
		transparency: number
	)
		local size, position
		if horizontal then
			size = Vector3.new(axisSize, ySize, thickness)
			position = Vector3.new(axisCenter, y, fixed)
		else
			size = Vector3.new(thickness, ySize, axisSize)
			position = Vector3.new(fixed, y, axisCenter)
		end
		Build.part({
			name = name,
			size = size,
			cf = CFrame.new(position),
			color = color,
			material = material,
			transparency = transparency,
			canCollide = transparency < 0.5,
			castShadow = false,
			parent = parent,
		})
	end

	local glassColor = Build.color(blueprint.colors.glass)
	local wt = blueprint.wallThickness
	piece("Cam", at, width - 0.5, centerY, height - 0.5, wt * 0.45, glassColor, Enum.Material.Glass, 0.72)
	-- Cerceve: dort kenar + orta dikme
	piece("PencereCercevesi", at, width, sill + 0.12, 0.28, wt + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at, width, sill + height - 0.12, 0.28, wt + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at - width * 0.5 + 0.14, 0.28, centerY, height, wt + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereCercevesi", at + width * 0.5 - 0.14, 0.28, centerY, height, wt + 0.1, frameColor, Enum.Material.SmoothPlastic, 0)
	piece("PencereKayidi", at, 0.18, centerY, height - 0.4, wt + 0.06, frameColor, Enum.Material.SmoothPlastic, 0)
	-- Denizlik: disariya tasan ince raf
	piece("Denizlik", at, width + 0.6, sill - 0.1, 0.22, wt + 0.8, frameColor, Enum.Material.SmoothPlastic, 0)
end

--- Tek bir duvari kapi ve pencere bosluklariyla birlikte kurar.
function Walls.build(blueprint: Blueprint, parent: Instance, wall: any, color: Color3, trim: Color3)
	local from, to = wall.from, wall.to
	local horizontal = math.abs(to[2] - from[2]) < EPSILON

	local axisFrom, axisTo, fixed
	if horizontal then
		axisFrom = math.min(from[1], to[1])
		axisTo = math.max(from[1], to[1])
		fixed = from[2]
	else
		axisFrom = math.min(from[2], to[2])
		axisTo = math.max(from[2], to[2])
		fixed = from[1]
	end

	-- Kapi ve pencere ayni eksende siralaniyor; duvar ikisinin arasinda
	-- kalan parcalardan kuruluyor.
	local openings = {}
	for _, door in wall.doors or {} do
		table.insert(openings, { at = door.at, width = door.width, kind = "door" })
	end
	for _, window in wall.windows or {} do
		table.insert(openings, {
			at = window.at,
			width = window.width,
			kind = "window",
			sill = window.sill,
			height = window.height,
		})
	end
	table.sort(openings, function(a, b)
		return a.at < b.at
	end)

	-- SUPURGELIK, GOVDE VE KORNIS UST USTE DEGIL, USTUSTE YIGILI.
	--
	-- Eskiden supurgelik ile kornis duvarin ICINE, tam ayni kalinlikta
	-- ve ayni yere konuyordu: iki yuzey birebir cakisiyordu ve Roblox'ta
	-- boyle bir cift titrer (z-fighting). Artik duvar govdesi supurgelik
	-- ile kornisin ARASINDA basliyor ve bitiyor; seritler de duvardan
	-- biraz TASIYOR (gercek bir supurgelik gibi), yani hicbir yuzey
	-- ayni duzlemde degil.
	local levels = blueprint.levels
	local skirtTop = levels.skirtHeight
	local corniceBottom = blueprint.wallHeight - levels.corniceHeight
	local trimDepth = blueprint.wallThickness + levels.trimBulge

	--- Bir duvar parcasi: supurgelik + govde + kornis bandi. Kapi ve
	--- pencere bosluklarinda yalnizca denk gelen bantlar ciziliyor.
	local function stack(from: number, to: number, yFrom: number, yTo: number)
		if to - from <= EPSILON or yTo - yFrom <= EPSILON then
			return
		end
		local low = math.max(yFrom, 0)
		local high = math.min(yTo, blueprint.wallHeight)
		if low < skirtTop then
			Walls.segment(blueprint, parent, horizontal, fixed, from, to, low, math.min(high, skirtTop), trim, "Supurgelik", trimDepth)
		end
		Walls.segment(
			blueprint, parent, horizontal, fixed, from, to,
			math.max(low, skirtTop), math.min(high, corniceBottom), color
		)
		if high > corniceBottom then
			Walls.segment(blueprint, parent, horizontal, fixed, from, to, math.max(low, corniceBottom), high, trim, "TavanKornisi", trimDepth)
		end
	end

	local frameColor = Build.color(blueprint.colors.windowFrame)
	local cursor = axisFrom
	for _, opening in openings do
		local left = opening.at - opening.width * 0.5
		local right = opening.at + opening.width * 0.5
		stack(cursor, left, 0, blueprint.wallHeight)

		if opening.kind == "door" then
			-- Kapinin USTU: supurgelik oraya UZANMIYOR. Uzansaydi
			-- kapinin onunden gecen bir serit olurdu.
			stack(left, right, blueprint.doorHeight, blueprint.wallHeight)
			doorFrame(blueprint, parent, horizontal, fixed, opening.at, opening.width, trim)
		else
			-- Pencerenin alti ve ustu duvar olarak devam ediyor.
			stack(left, right, 0, opening.sill)
			stack(left, right, opening.sill + opening.height, blueprint.wallHeight)
			windowGlass(blueprint, parent, horizontal, fixed, opening.at, opening.width, opening.sill, opening.height, frameColor)
		end
		cursor = right
	end
	stack(cursor, axisTo, 0, blueprint.wallHeight)
end

return Walls
