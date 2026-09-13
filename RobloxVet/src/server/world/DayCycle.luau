--!strict
-- DayCycle.luau
-- =============
-- Gunduz/gece dongusu ve gece nobeti.
--
-- SUNUCU SAATI TEK KAYNAK
-- -----------------------
-- `Lighting.ClockTime` sunucuda yaziliyor ve Roblox onu istemcilere
-- kendiliginden replike ediyor. Istemcinin kendi saatini isletmesi,
-- bir oyuncuda gece digerinde gunduz olmasi demek olurdu - ve ucret
-- carpani geceye baglandigi icin bu yalnizca gorsel bir hata degil,
-- oynanis hatasi olurdu.
--
-- Gece neden farkli?
-- ------------------
-- Gece "bos zaman" degil, BASKA BIR OYUN: hasta seyrek geliyor ama
-- ucret zamli, acil vaka orani yuksek. Oyuncu gece nobetini bilerek
-- secebilsin diye butun sayilar data/daycycle.json'da.
--
-- Gecis neden yumusak?
-- --------------------
-- Bir karede gunduzden geceye atlamak (ambient, sis, yildizlar) goze
-- carpiyor. `blend` degeri esiklerin etrafinda transitionHours boyunca
-- 0'dan 1'e gidiyor ve butun renkler bu oran uzerinden karisiyor.

local Lighting = game:GetService("Lighting")
local RunService = game:GetService("RunService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)

local plan = require(Data.daycycle)

local DayCycle = {}

local clock = plan.startClockTime
local running = false
local streetLamps: { PointLight } = {}
local baseBrightness: { [PointLight]: number } = {}
local lampHeads: { [PointLight]: BasePart } = {}
local lampHeadColor: { [PointLight]: Color3 } = {}
-- Gun sonu dinleyicileri. Personel maasi bunun uzerinden kesiliyor:
-- "gun" tanimini iki yerde tutmamak icin tek kaynak burasi.
local dayListeners: { (day: number) -> () } = {}
local dayCount = 0

--- Saatin 0..24 araligina sarmasi.
local function wrap(hour: number): number
	return hour % 24
end

--- "Gece ne kadar?" 0 = tam gunduz, 1 = tam gece. Esiklerin etrafinda
--- transitionHours boyunca yumusakca gidiyor.
local function nightBlend(hour: number): number
	local fade = math.max(plan.transitionHours, 0.01)
	local start = plan.nightStart
	local finish = plan.nightEnd

	-- Aksam: nightStart'tan once fade saat boyunca kararmaya basliyor.
	local toEvening = wrap(hour - (start - fade))
	if toEvening <= fade then
		return toEvening / fade
	end
	-- Sabah: nightEnd'den sonra fade saat boyunca aydinlaniyor.
	local afterMorning = wrap(hour - finish)
	if afterMorning <= fade then
		return 1 - afterMorning / fade
	end

	-- Aradaki tam gece / tam gunduz.
	local sinceStart = wrap(hour - start)
	local nightLength = wrap(finish - start)
	return if sinceStart < nightLength then 1 else 0
end

local function lerpColor(a: { number }, b: { number }, t: number): Color3
	return Build.color(a):Lerp(Build.color(b), t)
end

--- Isiklandirmayi o anki karisima gore ayarlar.
local function applyBlend(blend: number)
	local day, night = plan.day, plan.night
	Lighting.ClockTime = clock
	Lighting.Ambient = lerpColor(day.ambient, night.ambient, blend)
	Lighting.OutdoorAmbient = lerpColor(day.outdoorAmbient, night.outdoorAmbient, blend)
	Lighting.Brightness = day.brightness + (night.brightness - day.brightness) * blend
	Lighting.FogColor = lerpColor(day.fogColor, night.fogColor, blend)

	local sky = Lighting:FindFirstChild("Gokyuzu")
	if sky ~= nil and sky:IsA("Sky") then
		sky.StarCount = math.floor(day.starCount + (night.starCount - day.starCount) * blend)
	end

	-- Sokak lambalari geceyle birlikte yaniyor. Gunduz sifir
	-- parlaklikta duruyorlar: parcayi silip yeniden yaratmak yerine
	-- parlakligi kismak, isik butcesini de sabit tutuyor.
	for _, light in streetLamps do
		light.Brightness = (baseBrightness[light] or 1) * blend
		-- Fenerin kendisi de sonuyor: yanmayan bir lambanin cami gunduz
		-- parliyorsa "bozuk" duruyor.
		local head = lampHeads[light]
		local color = lampHeadColor[light]
		if head ~= nil and color ~= nil then
			head.Color = color:Lerp(Color3.fromRGB(52, 56, 66), 1 - blend)
		end
	end
end

--- Gun sonunda cagrilacak bir dinleyici ekler. Gun, saatin 24'u
--- gecip bastan basmasiyla biter.
function DayCycle.onDayEnded(listener: (day: number) -> ())
	table.insert(dayListeners, listener)
end

--- Gecen gun sayisi (SelfTest ve hata ayiklama icin).
function DayCycle.dayNumber(): number
	return dayCount
end

--- Kayitli sokak lambasi sayisi (SelfTest olcuyor: sifirsa ad
--- eslesmesi bozulmus ve geceleri hicbir lamba yanmayacak demektir).
function DayCycle.lampCount(): number
	return #streetLamps
end

--- Sokak lambalarini kaydeder. Klinik kurulduktan sonra cagriliyor.
function DayCycle.collectLamps(root: Instance)
	streetLamps = {}
	table.clear(baseBrightness)
	table.clear(lampHeads)
	table.clear(lampHeadColor)
	for _, item in root:GetDescendants() do
		if item:IsA("PointLight") and item.Name == "SokakLambasiIsik" then
			table.insert(streetLamps, item)
			baseBrightness[item] = item.Brightness
			local head = item.Parent
			if head ~= nil and head:IsA("BasePart") then
				lampHeads[item] = head
				lampHeadColor[item] = head.Color
			end
		end
	end
end

--- O anki saat (0..24).
function DayCycle.clockTime(): number
	return clock
end

--- Gece karisimi (0..1). Ucret ve hasta sikligi bunu kullaniyor.
function DayCycle.blend(): number
	return nightBlend(clock)
end

--- Gece mi? Yarisindan fazlasi geceyse evet.
function DayCycle.isNight(): boolean
	return nightBlend(clock) >= 0.5
end

--- O anki carpanlar. PatientFlow ve Economy bunlari okuyor; gunduzden
--- geceye GECERKEN de dogru olsun diye karisimla yumusatiliyor.
function DayCycle.modifiers(): { arrivalScale: number, feeMultiplier: number, xpMultiplier: number, emergencyBonus: number }
	local blend = nightBlend(clock)
	local day, night = plan.day, plan.night
	return {
		arrivalScale = day.arrivalScale + (night.arrivalScale - day.arrivalScale) * blend,
		feeMultiplier = day.feeMultiplier + (night.feeMultiplier - day.feeMultiplier) * blend,
		xpMultiplier = day.xpMultiplier + (night.xpMultiplier - day.xpMultiplier) * blend,
		emergencyBonus = day.emergencyChanceBonus
			+ (night.emergencyChanceBonus - day.emergencyChanceBonus) * blend,
	}
end

--- Istemciye yollanan goruntu (HUD saati ve nobet rozeti).
function DayCycle.snapshot(): any
	local blend = nightBlend(clock)
	return {
		clockTime = clock,
		night = blend >= 0.5,
		blend = blend,
		feeMultiplier = DayCycle.modifiers().feeMultiplier,
	}
end

--- Dongu. Saniyede 4 kez guncelliyor: ClockTime 12 dakikada bir tam tur
--- attigi icin her karede yazmak ayni degeri tekrar tekrar replike
--- etmekten baska bir sey yapmazdi.
function DayCycle.start()
	if running then
		return
	end
	running = true
	applyBlend(nightBlend(clock))

	local accumulator = 0
	local hoursPerSecond = 24 / (plan.dayMinutes * 60)
	RunService.Heartbeat:Connect(function(dt: number)
		-- dt tavanlaniyor: Studio duraklatilip devam ettirildiginde tek
		-- karede saatlerce zaman gecmesin.
		local advanced = clock + math.min(dt, 0.25) * hoursPerSecond
		if advanced >= 24 then
			-- Gun dondu: dinleyiciler ayri bir is parcaciginda cagriliyor
			-- ki biri hata verirse dongu durmasin.
			dayCount += 1
			local day = dayCount
			for _, listener in dayListeners do
				task.spawn(listener, day)
			end
		end
		clock = wrap(advanced)
		accumulator += dt
		if accumulator < 0.25 then
			return
		end
		accumulator = 0
		applyBlend(nightBlend(clock))
	end)
end

return DayCycle
