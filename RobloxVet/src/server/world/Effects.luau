--!strict
-- Effects.luau
-- ============
-- Tedavi ve olay efektleri: damla, buhar, isilti, toz.
--
-- Neden ParticleEmitter YOK?
-- --------------------------
-- Bir ParticleEmitter'in gorunumu `Texture` alanina bagli ve o bir
-- asset. Bu depoyu ureten ortamda Roblox'a ag erisimi yoktu; yuklenmeyen
-- bir doku, emitter'i sessizce BOS birakir - Studio acilmadan fark
-- edilmez. Tweenlenen kucuk parcalar hicbir asset istemiyor, olculeri
-- ve omru koddan belli, ve gorunumu burada okunabiliyor.
--
-- Maliyet
-- -------
-- Her efekt en fazla 10 parca yaratiyor ve omru dolunca kendini yok
-- ediyor. Parcalar carpismiyor, sorgulanmiyor, golge dusurmuyor:
-- fizik ve isik motoruna hic girmiyorlar.

local Debris = game:GetService("Debris")
local TweenService = game:GetService("TweenService")

local Effects = {}

local DEFAULT_LIFETIME = 0.9

local function spark(parent: Instance, cframe: CFrame, size: Vector3, color: Color3, material: Enum.Material?): BasePart
	local part = Instance.new("Part")
	part.Name = "Efekt"
	part.Size = size
	part.CFrame = cframe
	part.Color = color
	part.Material = material or Enum.Material.Neon
	part.Anchored = true
	part.CanCollide = false
	part.CanQuery = false
	part.CanTouch = false
	part.CastShadow = false
	part.Shape = Enum.PartType.Ball
	part.Transparency = 0.1
	part.Parent = parent
	return part
end

--- Bir parcayi hedefe tasiyip soldurur ve sonunda yok eder.
local function fly(part: BasePart, target: CFrame, seconds: number, endSize: Vector3?)
	local goal: { [string]: any } = { CFrame = target, Transparency = 1 }
	if endSize ~= nil then
		goal.Size = endSize
	end
	TweenService:Create(
		part,
		TweenInfo.new(seconds, Enum.EasingStyle.Quad, Enum.EasingDirection.Out),
		goal
	):Play()
	Debris:AddItem(part, seconds + 0.1)
end

--- Damla: yukaridan asagi dusen kucuk kureler (damla, ilac, su).
function Effects.drops(parent: Instance, origin: CFrame, color: Color3, count: number?)
	local total = count or 5
	for index = 1, total do
		local offset = CFrame.new(
			(math.random() - 0.5) * 1.2,
			1.4 + index * 0.12,
			(math.random() - 0.5) * 1.2
		)
		local part = spark(parent, origin * offset, Vector3.new(0.16, 0.22, 0.16), color, Enum.Material.Glass)
		part.Transparency = 0.25
		fly(part, origin * offset * CFrame.new(0, -1.5, 0), 0.45 + index * 0.05, Vector3.new(0.3, 0.06, 0.3))
	end
end

--- Buhar: yukari suzulup buyuyerek kaybolan yumusak kureler (yikama).
function Effects.steam(parent: Instance, origin: CFrame, color: Color3, count: number?)
	local total = count or 6
	for index = 1, total do
		local offset = CFrame.new(
			(math.random() - 0.5) * 1.8,
			0.4,
			(math.random() - 0.5) * 1.8
		)
		local part = spark(parent, origin * offset, Vector3.new(0.5, 0.5, 0.5), color, Enum.Material.SmoothPlastic)
		part.Transparency = 0.55
		fly(
			part,
			origin * offset * CFrame.new(0, 2.2 + index * 0.15, 0),
			1.1 + index * 0.06,
			Vector3.new(1.6, 1.6, 1.6)
		)
	end
end

--- Isilti: disari dogru acilan kucuk parlak noktalar (iyilesme, odul).
function Effects.sparkle(parent: Instance, origin: CFrame, color: Color3, count: number?)
	local total = count or 8
	for index = 1, total do
		local angle = index / total * math.pi * 2
		local part = spark(parent, origin * CFrame.new(0, 0.6, 0), Vector3.new(0.24, 0.24, 0.24), color)
		fly(
			part,
			origin * CFrame.new(math.cos(angle) * 1.9, 1.5 + math.sin(angle * 2) * 0.5, math.sin(angle) * 1.9),
			DEFAULT_LIFETIME,
			Vector3.new(0.05, 0.05, 0.05)
		)
	end
end

--- Halka: zeminden yukari acilan tek bir halka (ameliyat bitisi,
--- seviye atlama gibi "bir sey tamamlandi" anlari).
function Effects.ring(parent: Instance, origin: CFrame, color: Color3)
	local part = Instance.new("Part")
	part.Name = "EfektHalkasi"
	part.Shape = Enum.PartType.Cylinder
	part.Size = Vector3.new(0.12, 1.2, 1.2)
	-- Silindir X ekseninde uzuyor; yatay bir halka icin Z etrafinda
	-- 90 derece cevriliyor (Build.pillar ile ayni tuzak).
	part.CFrame = origin * CFrame.new(0, 0.35, 0) * CFrame.Angles(0, 0, math.pi / 2)
	part.Color = color
	part.Material = Enum.Material.Neon
	part.Anchored = true
	part.CanCollide = false
	part.CanQuery = false
	part.CanTouch = false
	part.CastShadow = false
	part.Transparency = 0.25
	part.Parent = parent

	TweenService:Create(
		part,
		TweenInfo.new(0.7, Enum.EasingStyle.Quint, Enum.EasingDirection.Out),
		{ Size = Vector3.new(0.05, 7, 7), Transparency = 1 }
	):Play()
	Debris:AddItem(part, 0.85)
end

-- Tedavi turu -> efekt. Bilinmeyen tedavi isiltiyla gecistiriliyor:
-- yeni bir tedavi eklendiginde efektsiz kalmasindansa genel bir efekt
-- gorunmesi dogru.
local KIND_COLORS = {
	injection = Color3.fromRGB(226, 104, 94),
	pill = Color3.fromRGB(240, 206, 96),
	bandage = Color3.fromRGB(238, 228, 210),
	drops = Color3.fromRGB(118, 184, 220),
	wash = Color3.fromRGB(206, 228, 238),
	feed = Color3.fromRGB(196, 154, 96),
	water = Color3.fromRGB(118, 184, 220),
	surgery = Color3.fromRGB(120, 226, 224),
}

--- Tedaviye uygun efekti oynatir.
function Effects.treatment(parent: Instance, origin: CFrame, treatmentId: string)
	local color = KIND_COLORS[treatmentId] or Color3.fromRGB(120, 226, 224)
	if treatmentId == "wash" then
		Effects.steam(parent, origin, color)
	elseif treatmentId == "drops" or treatmentId == "water" then
		Effects.drops(parent, origin, color)
	elseif treatmentId == "injection" or treatmentId == "pill" then
		Effects.drops(parent, origin, color, 3)
		Effects.sparkle(parent, origin, color, 5)
	elseif treatmentId == "surgery" then
		Effects.ring(parent, origin, color)
	else
		Effects.sparkle(parent, origin, color)
	end
end

--- Bilinen tedavi efektleri (SelfTest karsilastirmasi icin).
function Effects.knownTreatments(): { string }
	local ids = {}
	for id in KIND_COLORS do
		table.insert(ids, id)
	end
	table.sort(ids)
	return ids
end

return Effects
