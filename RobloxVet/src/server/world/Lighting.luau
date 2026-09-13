--!strict
-- Lighting.luau
-- =============
-- Isiklandirma ve son-islem efektleri. Butun sayilar
-- data/lighting.json'dan geliyor (konvansiyon 1: ayarlanabilir her sey
-- veride).
--
-- Neden bu kadar kisik?
-- ---------------------
-- Ilk surumde sahne goz yoruyordu. Suclu tek bir sayiydi:
-- `Bloom.Threshold = 1.05`. Bloom, esigin USTUNDEKI her pikseli
-- parlatir; 1.0 zaten "beyaz" demek oldugu icin bu esikte beyaz duvar,
-- beyaz dolap, beyaz onluk - hepsi parliyordu. Esik 2.0'a cikinca
-- yalnizca GERCEKTEN isik yayan yuzeyler (Neon) parliyor.
--
-- Ayni sebeple `Material.Neon` yalnizca uc yerde: tavan lambasi
-- difuzoru, ameliyat lambasi merceği ve tabeladaki hac. Monitor ve
-- rontgen EKRANLARI Neon degil - gercek bir ekran karanlik odada bile
-- etrafini aydinlatmaz.
--
-- Lighting.Technology neden burada yok?
-- -------------------------------------
-- Technology betikten yazilamiyor (scriptable degil); yer dosyasinin
-- XML'inde ayarlaniyor (tools/rbxlx/tree.py).

local Lighting = game:GetService("Lighting")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)

local config = require(Data.lighting)

local Sky = {}

--- Ayni efekti iki kez eklemeyi onler (Studio'da Play'e ikinci kez
--- basildiginda Lighting sifirlanmayabiliyor).
local function replace(className: string, name: string): Instance
	local existing = Lighting:FindFirstChild(name)
	if existing ~= nil then
		existing:Destroy()
	end
	local instance = Instance.new(className)
	instance.Name = name
	instance.Parent = Lighting
	return instance
end

--- Bir lamba ayarini veriden getirir. Bilinmeyen ad sessizce yutulmaz.
function Sky.lamp(name: string): { brightness: number, range: number, color: Color3, angle: number? }
	local entry = config.lights[name]
	if entry == nil then
		warn(("[Lighting] bilinmeyen lamba ayari: '%s' (data/lighting.json)"):format(name))
		return { brightness = 1, range = 16, color = Color3.new(1, 1, 1) }
	end
	return {
		brightness = entry.brightness,
		range = entry.range,
		color = Build.color(entry.color),
		angle = entry.angle,
	}
end

function Sky.apply()
	Lighting.Ambient = Build.color(config.ambient)
	Lighting.OutdoorAmbient = Build.color(config.outdoorAmbient)
	Lighting.Brightness = config.brightness
	Lighting.ClockTime = config.clockTime
	Lighting.GeographicLatitude = config.geographicLatitude
	Lighting.ExposureCompensation = config.exposureCompensation
	Lighting.EnvironmentDiffuseScale = config.environmentDiffuseScale
	Lighting.EnvironmentSpecularScale = config.environmentSpecularScale
	Lighting.GlobalShadows = true
	Lighting.ShadowSoftness = config.shadowSoftness
	Lighting.FogStart = config.fogStart
	Lighting.FogEnd = config.fogEnd
	Lighting.FogColor = Build.color(config.fogColor)

	local atmosphere = replace("Atmosphere", "Atmosfer") :: Atmosphere
	atmosphere.Density = config.atmosphere.density
	atmosphere.Offset = config.atmosphere.offset
	atmosphere.Color = Build.color(config.atmosphere.color)
	atmosphere.Decay = Build.color(config.atmosphere.decay)
	atmosphere.Glare = config.atmosphere.glare
	atmosphere.Haze = config.atmosphere.haze

	local bloom = replace("BloomEffect", "Parlama") :: BloomEffect
	bloom.Intensity = config.bloom.intensity
	bloom.Size = config.bloom.size
	bloom.Threshold = config.bloom.threshold

	local color = replace("ColorCorrectionEffect", "RenkDuzeltme") :: ColorCorrectionEffect
	color.Brightness = config.colorCorrection.brightness
	color.Contrast = config.colorCorrection.contrast
	color.Saturation = config.colorCorrection.saturation
	color.TintColor = Build.color(config.colorCorrection.tint)

	local sun = replace("SunRaysEffect", "GunesIsinlari") :: SunRaysEffect
	sun.Intensity = config.sunRays.intensity
	sun.Spread = config.sunRays.spread

	local depth = replace("DepthOfFieldEffect", "AlanDerinligi") :: DepthOfFieldEffect
	depth.FarIntensity = config.depthOfField.farIntensity
	depth.NearIntensity = config.depthOfField.nearIntensity
	depth.FocusDistance = config.depthOfField.focusDistance
	depth.InFocusRadius = config.depthOfField.inFocusRadius

	local sky = replace("Sky", "Gokyuzu") :: Sky
	sky.StarCount = config.sky.starCount
	sky.SunAngularSize = config.sky.sunAngularSize
end

return Sky
