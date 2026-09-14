--!strict
-- Geometry.luau
-- =============
-- Kurulmus dunyayi OLCEN denetim: hangi parcalar gercekten ic ice?
--
-- Neden gerekti?
-- --------------
-- `tools/verify_layout.py` esyalarin YER BUTCELERINI olcuyor ve
-- yapisal katmanlarin Y bantlarini denetliyor. Ikisi de plandan
-- okuyor - yani BENIM PLANIMI denetliyor, kurulan dunyayi degil.
-- Aradaki farki oyuncu gordu: cimen zemin butun haritayi kapliyordu ve
-- bina dosemelerinin icinden geciyordu. Plan dogruydu; kurulan sey
-- degildi.
--
-- Burasi o acigi kapatiyor: oyun kendi kurdugu parcalari, gercek
-- CFrame ve Size degerleriyle olcuyor. Plan ile kod arasinda bir kayma
-- olursa burada goruluyor.
--
-- Neden yalnizca CARPISAN parcalar?
-- ---------------------------------
-- Detay parcalari (`CanCollide = false`) BILEREK ic ice: bir vida
-- panelin icinde durur, bir yaprak dalin icinden cikar, supurgelik
-- duvardan tasar. Onlari "hata" saymak, denetimi yuzlerce sahte
-- uyariyla doldurup ise yaramaz hale getirirdi.
--
-- Carpisan parcalar ise oyuncunun icinden GECEMEDIGI, gozle gordugu
-- kati govde: duvar, doseme, tavan, cati, zemin, masa govdesi. Onlarin
-- ic ice girmesi her zaman hatadir.
--
-- Neden kutu %2 kuculuyor?
-- ------------------------
-- Katmanlar bilerek BIRBIRINE DEGIYOR (dosemenin ustu = karonun alti).
-- Degen iki yuzey, kayan nokta hesabinda "kesisiyor" gorunebilir.
-- Kutuyu biraz kucultunce degme kesisme sayilmiyor, gercek girisim ise
-- hala yakalaniyor.

local Workspace = game:GetService("Workspace")

local Geometry = {}

export type Hit = {
	a: BasePart,
	b: BasePart,
	depth: number,
}

-- Bundan kucuk girisimler degme sayiliyor.
local SHRINK = 0.98
local MIN_DEPTH = 0.05

--- Iki eksen hizali kutunun en kucuk eksendeki girisim derinligi.
--- Donmus parcalar icin bu deger KABA kalir; o yuzden yalnizca
--- eksen hizali (donmemis) parcalar olculuyor.
local function penetration(a: BasePart, b: BasePart): number
	local aMin, aMax = a.Position - a.Size * 0.5, a.Position + a.Size * 0.5
	local bMin, bMax = b.Position - b.Size * 0.5, b.Position + b.Size * 0.5
	local x = math.min(aMax.X, bMax.X) - math.max(aMin.X, bMin.X)
	local y = math.min(aMax.Y, bMax.Y) - math.max(aMin.Y, bMin.Y)
	local z = math.min(aMax.Z, bMax.Z) - math.max(aMin.Z, bMin.Z)
	return math.min(x, y, z)
end

--- Parca eksen hizali mi? (Donmus parcalarda kutu olcumu yaniltir.)
local function axisAligned(part: BasePart): boolean
	local _, _, _, r00, r01, r02, r10, r11, r12, r20, r21, r22 = part.CFrame:GetComponents()
	for _, value in { r00, r01, r02, r10, r11, r12, r20, r21, r22 } do
		local rounded = math.round(value)
		if math.abs(value - rounded) > 1e-3 or math.abs(rounded) > 1 then
			return false
		end
	end
	return true
end

--- Bir modeldeki KATI govdenin kendi icinde kesisip kesismedigini
--- olcer. Donen liste bos degilse gercek bir modelleme hatasi var.
---
--- `ignore(a, b)` verilirse o cift atlaniyor: bilerek ic ice duran
--- (ornegin kapi kanadi ile cercevesi) ciftler icin.
function Geometry.overlaps(root: Instance, limit: number?, ignore: ((BasePart, BasePart) -> boolean)?): { Hit }
	local solids: { BasePart } = {}
	for _, item in root:GetDescendants() do
		if item:IsA("BasePart") and item.CanCollide and item.Transparency < 0.95 and axisAligned(item) then
			table.insert(solids, item)
		end
	end

	local params = OverlapParams.new()
	params.FilterType = Enum.RaycastFilterType.Include
	params.FilterDescendantsInstances = { root }
	params.MaxParts = 24

	local hits: { Hit } = {}
	local seen: { [string]: boolean } = {}
	local cap = limit or 12

	for _, part in solids do
		local found = Workspace:GetPartBoundsInBox(part.CFrame, part.Size * SHRINK, params)
		for _, other in found do
			if other == part or not other.CanCollide or other.Transparency >= 0.95 then
				continue
			end
			if not axisAligned(other) then
				continue
			end
			local depth = penetration(part, other)
			if depth < MIN_DEPTH then
				continue
			end
			if ignore ~= nil and (ignore(part, other) or ignore(other, part)) then
				continue
			end
			-- Her cift bir kez: adres sirasi sabit bir anahtar veriyor.
			local first, second = part, other
			if tostring(first) > tostring(second) then
				first, second = second, first
			end
			local key = first:GetFullName() .. "|" .. second:GetFullName()
			if seen[key] then
				continue
			end
			seen[key] = true
			table.insert(hits, { a = first, b = second, depth = depth })
			if #hits >= cap then
				return hits
			end
		end
	end
	return hits
end

--- Insan okunur tek satir: "Duvar ile Doseme_reception 0,50 stud".
function Geometry.describe(hit: Hit): string
	return ("%s <-> %s  (%.2f stud)"):format(hit.a.Name, hit.b.Name, hit.depth)
end

--- Olculen kati govde sayisi (denetimin GERCEKTEN bir sey olctugunu
--- gostermek icin: sifir parca olcen bir denetim her zaman "gecer").
function Geometry.solidCount(root: Instance): number
	local total = 0
	for _, item in root:GetDescendants() do
		if item:IsA("BasePart") and item.CanCollide and item.Transparency < 0.95 and axisAligned(item) then
			total += 1
		end
	end
	return total
end

return Geometry
