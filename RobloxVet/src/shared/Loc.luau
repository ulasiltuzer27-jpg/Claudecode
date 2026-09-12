--!strict
-- Loc.luau
-- ========
-- Cevirinin TEK erisim noktasi.
--
-- Eksik anahtar GIZLENMEZ
-- -----------------------
-- Bir anahtar tabloda yoksa anahtarin KENDISI kose parantez icinde
-- gosterilir: `[hud.money]`. Sessizce bos string dondurmek, eksik
-- cevirinin ekranda hic fark edilmemesine yol acardi. Depo genelindeki
-- kural bu (bkz. C# tarafinda Localization/Loc.cs).

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local table_tr = require(Data.locale.tr)

local Loc = {}

local strings: { [string]: string } = table_tr.strings

--- Anahtari cevirir. `{1}`, `{2}` ... yer tutucularini sirayla doldurur.
---
---     Loc.get("hud.money", 1250)      --> "PARA: 1250 TL"
---     Loc.get("yok.olan.anahtar")     --> "[yok.olan.anahtar]"
function Loc.get(key: string, ...: any): string
	local text = strings[key]
	if text == nil then
		return "[" .. key .. "]"
	end

	local count = select("#", ...)
	if count == 0 then
		return text
	end

	local args = table.pack(...)
	return (string.gsub(text, "{(%d+)}", function(index: string): string
		local position = tonumber(index)
		if position == nil or position < 1 or position > args.n then
			-- Yer tutucu var ama karsiligi verilmemis: bunu da gizleme.
			return "{" .. index .. "}"
		end
		return tostring(args[position])
	end))
end

--- Anahtar var mi? Denetleyiciler ve self-test icin.
function Loc.has(key: string): boolean
	return strings[key] ~= nil
end

--- Tablodaki toplam satir sayisi. SelfTest raporunda kullaniliyor.
function Loc.count(): number
	local total = 0
	for _ in strings do
		total += 1
	end
	return total
end

return Loc
