--!strict
-- Lighting.luau
-- =============
-- Isiklandirma ve son-islem efektleri.
--
-- Lighting.Technology neden burada yok?
-- -------------------------------------
-- Technology betikten yazilamiyor (scriptable degil); yer dosyasinin
-- XML'inde ayarlaniyor (tools/rbxlx/tree.py). Orasi Studio olmadan
-- dogrulayamadigimiz TEK deger; yanlis cikarsa isiklandirma baska bir
-- moda duser ve oyun calismaya devam eder. README kullaniciya tek
-- tiklik duzeltmeyi (Lighting -> Technology -> Future) soyluyor.
--
-- Buradaki her sey calisma aninda kurulabilen seyler: renkler, saat,
-- atmosfer ve efektler.

local Lighting = game:GetService("Lighting")

local Sky = {}

--- Ayni efekti iki kez eklemeyi onler (Play'e ikinci kez basildiginda
--- Studio Lighting'i sifirlamayabiliyor).
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

function Sky.apply()
	Lighting.Ambient = Color3.fromRGB(96, 100, 110)
	Lighting.OutdoorAmbient = Color3.fromRGB(128, 136, 150)
	Lighting.Brightness = 2.4
	Lighting.ClockTime = 14.2
	Lighting.GeographicLatitude = 38
	Lighting.ExposureCompensation = 0.12
	Lighting.EnvironmentDiffuseScale = 0.45
	Lighting.EnvironmentSpecularScale = 0.35
	Lighting.GlobalShadows = true
	Lighting.ShadowSoftness = 0.35
	Lighting.FogEnd = 1400
	Lighting.FogStart = 320
	Lighting.FogColor = Color3.fromRGB(196, 212, 226)

	local atmosphere = replace("Atmosphere", "Atmosfer") :: Atmosphere
	atmosphere.Density = 0.32
	atmosphere.Offset = 0.2
	atmosphere.Color = Color3.fromRGB(202, 214, 228)
	atmosphere.Decay = Color3.fromRGB(122, 140, 162)
	atmosphere.Glare = 0.18
	atmosphere.Haze = 1.1

	local bloom = replace("BloomEffect", "Parlama") :: BloomEffect
	bloom.Intensity = 0.55
	bloom.Size = 22
	bloom.Threshold = 1.05

	local color = replace("ColorCorrectionEffect", "RenkDuzeltme") :: ColorCorrectionEffect
	color.Brightness = 0.015
	color.Contrast = 0.09
	color.Saturation = 0.1
	color.TintColor = Color3.fromRGB(255, 252, 248)

	local sun = replace("SunRaysEffect", "GunesIsinlari") :: SunRaysEffect
	sun.Intensity = 0.06
	sun.Spread = 0.55

	-- Alan derinligi bilincli olarak ZAYIF: klinikte oyuncu surekli
	-- yakin nesnelere bakiyor, guclu bir bulaniklik arayuzu okunmaz
	-- hale getirirdi.
	local depth = replace("DepthOfFieldEffect", "AlanDerinligi") :: DepthOfFieldEffect
	depth.FarIntensity = 0.08
	depth.NearIntensity = 0
	depth.FocusDistance = 26
	depth.InFocusRadius = 42

	local sky = replace("Sky", "Gokyuzu") :: Sky
	sky.StarCount = 0
	sky.SunAngularSize = 16
end

return Sky
