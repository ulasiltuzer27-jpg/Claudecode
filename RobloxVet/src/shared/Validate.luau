--!strict
-- Validate.luau
-- =============
-- Istemciden gelen HER argümanin dogrulandigi yer.
--
-- SUNUCU OTORITER
-- ---------------
-- Istemci yalnizca niyet bildirir ("su aleti kullan"); sonucu sunucu
-- hesaplar. Buradaki denetimler o niyetin bicimini dogruluyor: tip,
-- uzunluk, bilinen kimlik, hiz limiti. Bicimi gecen bir istek hala
-- oyun kurallarina takilabilir (parasi yetmiyor, alet acilmamis) -
-- o denetimler ilgili modullerde.

local Net = require(script.Parent.Net)

local Validate = {}

-- Kimlik dizeleri: yalnizca harf/rakam, makul uzunluk. Istemcinin
-- 100 KB'lik bir dize yollayip sunucuyu mesgul etmesini engeller.
local MAX_ID_LENGTH = 48

--- Kimlik dizesi mi?
function Validate.id(value: any): boolean
	if type(value) ~= "string" then
		return false
	end
	if #value == 0 or #value > MAX_ID_LENGTH then
		return false
	end
	return string.match(value, "^[%a][%w_]*$") ~= nil
end

--- Sonlu sayi mi? (NaN ve sonsuz elenir - ikisi de aritmetigi bozar.)
function Validate.number(value: any): boolean
	if type(value) ~= "number" then
		return false
	end
	return value == value and value ~= math.huge and value ~= -math.huge
end

--- Tam sayi ve araliginda mi?
function Validate.integerInRange(value: any, low: number, high: number): boolean
	if not Validate.number(value) then
		return false
	end
	if value % 1 ~= 0 then
		return false
	end
	return value >= low and value <= high
end

-- ── Hiz limiti ────────────────────────────────────────────────────────
-- Oyuncu basina, remote basina son cagrilarin zaman damgasi.
local buckets: { [Player]: { [string]: { count: number, windowStart: number } } } = {}

--- Oyuncu bu remote'u cagirabilir mi? Limiti asmissa false.
function Validate.rateOk(player: Player, remoteName: string): boolean
	local limit = Net.RateLimit[remoteName]
	if limit == nil then
		return true
	end

	local playerBucket = buckets[player]
	if playerBucket == nil then
		playerBucket = {}
		buckets[player] = playerBucket
	end

	local now = os.clock()
	local entry = playerBucket[remoteName]
	if entry == nil or now - entry.windowStart >= 1 then
		playerBucket[remoteName] = { count = 1, windowStart = now }
		return true
	end

	if entry.count >= limit then
		return false
	end
	entry.count += 1
	return true
end

--- Oyuncu ayrilinca kovasini temizler. Aksi halde tablo sizinti yapar.
function Validate.forget(player: Player)
	buckets[player] = nil
end

return Validate
