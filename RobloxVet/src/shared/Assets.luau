--!strict
-- Assets.luau
-- ===========
-- Creator Store asset ID'lerinin TEK okuma noktasi.
--
-- Bu depo, Roblox'a ag erisimi OLMAYAN bir ortamda uretildi: hicbir
-- asset ID'nin gercekten var oldugu dogrulanamadi. Uydurma ID yazmak
-- oyunda gri/kirik model demek olurdu. Bu yuzden data/assetIds.json
-- bilerek BOS geliyor ve butun modeller kod ile uretiliyor.
--
-- Kullanici bir ID yapistirdiginda burasi onu dondurur, ilgili yapi
-- prosedurel modelin yerine mesh'i kullanir. Yuva 0 ise nil doner ve
-- prosedurel model kullanilir - yani tablo bos oldugunda hicbir sey
-- bozulmaz.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local ids = require(Data.assetIds)

local Assets = {}

local function resolve(group: { [string]: number }?, name: string): string?
	if group == nil then
		return nil
	end
	local id = group[name]
	if id == nil or id == 0 then
		return nil
	end
	return "rbxassetid://" .. tostring(id)
end

--- Mesh yuvasi. nil -> prosedurel model kullanilacak.
function Assets.mesh(name: string): string?
	return resolve(ids.meshes, name)
end

--- Ses yuvasi. nil -> ses calinmayacak (sessizlik, hata degil).
function Assets.sound(name: string): string?
	return resolve(ids.sounds, name)
end

--- Gorsel yuvasi. nil -> arayuz metin/sekil ile idare edecek.
function Assets.image(name: string): string?
	return resolve(ids.images, name)
end

--- Bir sesi verilen yere kurar. Yuva bossa nil doner; cagiran taraf
--- bunu kontrol etmek zorunda degil.
function Assets.playSound(name: string, parent: Instance, volume: number?): Sound?
	local assetId = Assets.sound(name)
	if assetId == nil then
		return nil
	end
	local sound = Instance.new("Sound")
	sound.SoundId = assetId
	sound.Volume = volume or 0.5
	sound.Parent = parent
	sound:Play()
	sound.Ended:Once(function()
		sound:Destroy()
	end)
	return sound
end

--- Kac yuva dolu? SelfTest raporunda kullaniciya gosteriliyor.
function Assets.filledCount(): number
	local total = 0
	for _, group in { ids.meshes, ids.sounds, ids.images } do
		if group ~= nil then
			for _, id in group do
				if id ~= 0 then
					total += 1
				end
			end
		end
	end
	return total
end

return Assets
