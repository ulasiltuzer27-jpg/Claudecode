--!strict
-- Audio.luau
-- ==========
-- Sesin ve muzigin TEK erisim noktasi (konvansiyon 2, Assets.luau ile
-- ayni fikir). Butun sesler data/audio.json'dan geliyor.
--
-- Hazir sesler nereden geliyor?
-- -----------------------------
-- `rbxasset://` yollari Roblox istemcisinin ICINDE gelir: ID gerektirmez,
-- internetten indirilmez, Creator Store'a bagli degildir. Bu depoyu
-- ureten ortamda Roblox'a erisim yoktu, o yuzden bu yollarin HEPSININ
-- dogru oldugu dogrulanamadi.
--
-- Bu yuzden sistem KENDINI TESHIS EDIYOR: yuklenemeyen her ses bir kez
-- Output'a adiyla yaziliyor (`[SES] 'uiClick' yuklenemedi`). Sessiz bir
-- gizem yerine, tek satirlik bir duzeltme.
--
-- MUZIK icin hazir parca yok. `music` yuvalari bos geliyor; kullanici
-- data/audio.json'a bir assetId yazinca calisiyor.
--
-- Ses seviyeleri
-- --------------
-- Iki SoundGroup: Muzik ve Efekt. Ana seviye ikisiyle CARPILIYOR
-- (SoundGroup ic ice koymak yerine) - daha az hareketli parca, ayni sonuc.

local ContentProvider = game:GetService("ContentProvider")
local RunService = game:GetService("RunService")
local SoundService = game:GetService("SoundService")
local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local config = require(Data.audio)

local Audio = {}

local MUSIC_GROUP = "VetMuzik"
local SFX_GROUP = "VetEfekt"

local volumes = { master = 0.8, music = 0.5, sfx = 0.8 }
local failed: { [string]: boolean } = {}
local reported = false
local currentTrack: string? = nil
local currentSound: Sound? = nil

local function group(name: string, fallbackVolume: number): SoundGroup
	local existing = SoundService:FindFirstChild(name)
	if existing ~= nil and existing:IsA("SoundGroup") then
		return existing
	end
	local created = Instance.new("SoundGroup")
	created.Name = name
	created.Volume = fallbackVolume
	created.Parent = SoundService
	return created
end

--- Bir ipucunun calinacak icerigi. assetId doluysa o kazanir.
local function contentOf(cue: any): string?
	if cue.assetId ~= nil and cue.assetId ~= 0 then
		return "rbxassetid://" .. tostring(cue.assetId)
	end
	if type(cue.sound) == "string" and #cue.sound > 0 then
		return cue.sound
	end
	return nil
end

--- Ses seviyelerini uygular. Ayarlar panelinden cagriliyor.
function Audio.setVolumes(settings: any)
	if type(settings) ~= "table" then
		return
	end
	if type(settings.masterVolume) == "number" then
		volumes.master = math.clamp(settings.masterVolume, 0, 1)
	end
	if type(settings.musicVolume) == "number" then
		volumes.music = math.clamp(settings.musicVolume, 0, 1)
	end
	if type(settings.sfxVolume) == "number" then
		volumes.sfx = math.clamp(settings.sfxVolume, 0, 1)
	end

	group(MUSIC_GROUP, 0).Volume = volumes.master * volumes.music
	group(SFX_GROUP, 0).Volume = volumes.master * volumes.sfx
end

--- Tek seferlik ses. `parent` verilirse 3B (o parcadan duyulur),
--- verilmezse 2B arayuz sesi.
---
--- Bilinmeyen ipucu sessizce yutulmuyor: veri dosyasindan silinmis bir
--- sesin cagrisi Output'a dusuyor.
function Audio.play(cueId: string, parent: Instance?): Sound?
	local cue = config.cues[cueId]
	if cue == nil then
		warn(("[SES] bilinmeyen ipucu: '%s' (data/audio.json)"):format(cueId))
		return nil
	end

	local content = contentOf(cue)
	if content == nil then
		return nil -- yuva bos: sessizlik, hata degil
	end

	local sound = Instance.new("Sound")
	sound.Name = cueId
	sound.SoundId = content
	sound.Volume = cue.volume
	sound.PlaybackSpeed = cue.pitch
	sound.RollOffMaxDistance = 90
	sound.RollOffMinDistance = 8
	sound.SoundGroup = group(SFX_GROUP, volumes.master * volumes.sfx)
	sound.Parent = parent or SoundService

	-- Yuklenmeyen ses, ilk kullanimda bir kez raporlaniyor.
	task.delay(3, function()
		if sound.Parent ~= nil and not sound.IsLoaded and not failed[cueId] then
			failed[cueId] = true
			warn(("[SES] '%s' yuklenemedi (%s) - data/audio.json'dan degistirin"):format(cueId, content))
		end
	end)

	sound:Play()
	sound.Ended:Once(function()
		sound:Destroy()
	end)
	-- Yuklenemeyen ses Ended yayinlamaz; bellekte birikmesin.
	task.delay(12, function()
		if sound.Parent ~= nil then
			sound:Destroy()
		end
	end)
	return sound
end

--- Dongusel ortam sesi. Ayni parcada ayni ipucu iki kez kurulmaz.
function Audio.ambience(cueId: string, parent: Instance): Sound?
	local existing = parent:FindFirstChild(cueId)
	if existing ~= nil and existing:IsA("Sound") then
		return existing
	end

	local cue = config.cues[cueId]
	if cue == nil then
		warn(("[SES] bilinmeyen ortam sesi: '%s'"):format(cueId))
		return nil
	end
	local content = contentOf(cue)
	if content == nil then
		return nil
	end

	local sound = Instance.new("Sound")
	sound.Name = cueId
	sound.SoundId = content
	sound.Volume = cue.volume
	sound.PlaybackSpeed = cue.pitch
	sound.Looped = true
	sound.RollOffMaxDistance = 120
	sound.SoundGroup = group(SFX_GROUP, volumes.master * volumes.sfx)
	sound.Parent = parent
	sound:Play()
	return sound
end

function Audio.stopAmbience(cueId: string, parent: Instance)
	local existing = parent:FindFirstChild(cueId)
	if existing ~= nil and existing:IsA("Sound") then
		existing:Destroy()
	end
end

--- Muzik parcasi. Ayni parca isteniyorsa hicbir sey yapmiyor; farkli
--- parcaya CAPRAZ GECIS yapiyor (ani kesme kulagi tirmaliyor).
function Audio.music(trackId: string?)
	assert(RunService:IsClient(), "Audio.music yalnizca istemcide - muzik oyuncuya ozel")

	if trackId == currentTrack then
		return
	end
	currentTrack = trackId

	local fade = config.crossfadeSeconds
	local previous = currentSound
	if previous ~= nil then
		local out = TweenService:Create(previous, TweenInfo.new(fade), { Volume = 0 })
		out:Play()
		out.Completed:Once(function()
			previous:Destroy()
		end)
		currentSound = nil
	end

	if trackId == nil then
		return
	end

	local track = config.music[trackId]
	if track == nil then
		warn(("[SES] bilinmeyen muzik parcasi: '%s'"):format(trackId))
		return
	end
	if track.assetId == 0 then
		-- Yuva bos: muzik yok. Kullaniciya bir kez hatirlat.
		if not failed["music:" .. trackId] then
			failed["music:" .. trackId] = true
			print(("[SES] '%s' muzigi bos - data/audio.json > music > %s > assetId"):format(trackId, trackId))
		end
		return
	end

	local sound = Instance.new("Sound")
	sound.Name = "Muzik_" .. trackId
	sound.SoundId = "rbxassetid://" .. tostring(track.assetId)
	sound.Volume = 0
	sound.Looped = true
	sound.SoundGroup = group(MUSIC_GROUP, volumes.master * volumes.music)
	sound.Parent = SoundService
	sound:Play()
	TweenService:Create(sound, TweenInfo.new(fade), { Volume = track.volume }):Play()
	currentSound = sound
end

--- Butun hazir sesleri onceden yukler ve tutmayanlari TEK SEFERDE
--- raporlar. Istemci acilista bir kez cagiriyor.
function Audio.preload()
	assert(RunService:IsClient(), "Audio.preload yalnizca istemcide")
	if reported then
		return
	end
	reported = true

	task.spawn(function()
		local sounds: { Instance } = {}
		local names: { [Instance]: string } = {}
		for cueId, cue in config.cues do
			local content = contentOf(cue)
			if content == nil then
				continue
			end
			local probe = Instance.new("Sound")
			probe.SoundId = content
			probe.Parent = SoundService
			table.insert(sounds, probe)
			names[probe] = cueId
		end

		pcall(function()
			ContentProvider:PreloadAsync(sounds)
		end)

		local broken = {}
		for _, probe in sounds do
			if probe:IsA("Sound") and not probe.IsLoaded then
				table.insert(broken, names[probe])
			end
			probe:Destroy()
		end

		if #broken == 0 then
			print(("[SES] %d ses hazir."):format(#sounds))
		else
			table.sort(broken)
			warn(("[SES] %d/%d ses yuklenemedi - data/audio.json'dan degistirilebilir:"):format(#broken, #sounds))
			warn("  " .. table.concat(broken, ", "))
		end
	end)
end

--- Kac ipucu tanimli? SelfTest raporunda kullaniliyor.
function Audio.cueCount(): number
	local total = 0
	for _ in config.cues do
		total += 1
	end
	return total
end

function Audio.hasCue(cueId: string): boolean
	return config.cues[cueId] ~= nil
end

return Audio
