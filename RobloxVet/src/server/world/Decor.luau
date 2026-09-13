--!strict
-- Decor.luau
-- ==========
-- Klinik dekorasyonu: duvar/zemin temasi ve sus esyalari.
--
-- Klinik ORTAK
-- ------------
-- Bina tek ve paylasili; "kimin temasi uygulanacak" sorusunun bir cevabi
-- olmak zorunda. Kural, yukseltmelerdeki CLINIC_WIDE kuralinin aynisi:
-- ONLINE oyunculardan en iyisi kazaniyor - temada `order` en buyuk olan,
-- sus esyasinda ise "birinin sahip olmasi yeter".
--
-- Sus esyalari KURULUSTA insa ediliyor
-- ------------------------------------
-- Satin alininca yeni parca yaratmak, yerlesim denetiminden
-- (tools/verify_layout.py) kacan bir model demek olurdu: o esya hicbir
-- cakisma kontrolunden gecmezdi. Bunun yerine hepsi bastan kuruluyor ve
-- yalnizca GORUNURLUGU aciliyor. Bedeli birkac yuz gorunmez parca;
-- kazanci, dekorun de butun denetimlerden gecmesi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)

local decorData = require(Data.decor)
local plan = require(Data.clinic)

local Decor = {}

-- Parca adi -> temanin hangi rengi. Clinic.luau parcalari bu adlarla
-- kuruyor; ad degisirse tema sessizce uygulanmaz, bu yuzden
-- SelfTest boyanan parca sayisini olcuyor.
local ROLE_BY_NAME: { [string]: string } = {
	Duvar = "wall",
	Supurgelik = "wallTrim",
	TavanKornisi = "wallTrim",
	KapiCercevesi = "accent",
	Karo = "floorTile",
}

local themeById: { [string]: any } = {}
for _, theme in decorData.themes do
	themeById[theme.id] = theme
end

local propById: { [string]: any } = {}
for _, item in decorData.props do
	propById[item.id] = item
end

-- Kuruluşta toplanan tutamaklar.
local painted: { [string]: { BasePart } } = {}
local propModels: { [string]: Model } = {}
local currentTheme = ""

--- Varsayilan tema: `order` degeri en kucuk olan (bedava olan).
local function defaultTheme(): any
	local best = nil
	for _, theme in decorData.themes do
		if best == nil or theme.order < best.order then
			best = theme
		end
	end
	return best
end

--- Bir parcanin rolunu adindan cikarir. Doseme adlari oda kimligini
--- tasidigi icin ayrica ele aliniyor.
local function roleOf(part: BasePart): string?
	local role = ROLE_BY_NAME[part.Name]
	if role ~= nil then
		return role
	end
	if string.match(part.Name, "^Doseme_") ~= nil then
		return "floor"
	end
	return nil
end

--- Klinik kurulduktan sonra cagriliyor: boyanacak parcalari toplar ve
--- sus esyalarini gorunmez olarak kurar.
function Decor.attach(structure: Instance, furniture: Instance, buildProp: (string, CFrame, Instance) -> Model?)
	painted = {}
	propModels = {}
	currentTheme = ""

	for _, descendant in structure:GetDescendants() do
		if not descendant:IsA("BasePart") then
			continue
		end
		local role = roleOf(descendant)
		if role == nil then
			continue
		end
		local list = painted[role]
		if list == nil then
			list = {}
			painted[role] = list
		end
		table.insert(list, descendant)
	end

	local roomOf: { [string]: any } = {}
	for _, room in plan.rooms do
		roomOf[room.id] = room
	end

	for _, item in decorData.props do
		local room = roomOf[item.room]
		if room == nil then
			warn(("[Decor] '%s' bilinmeyen odaya konuyor: %s"):format(item.id, item.room))
			continue
		end
		local origin = CFrame.new(item.at[1], plan.floorY, item.at[2])
			* CFrame.Angles(0, math.rad(item.rot or 0), 0)
		local model = buildProp(item.prop, origin, furniture)
		if model == nil then
			continue
		end
		model.Name = "Dekor_" .. item.id
		propModels[item.id] = model
		-- Bastan gorunmez: sahip olunmayan sus ortada durmamali.
		for _, part in model:GetDescendants() do
			if part:IsA("BasePart") then
				part.Transparency = 1
				part.CanCollide = false
			end
		end
	end

	-- Varsayilan tema hemen uygulaniyor; oyuncu girmeden once de klinik
	-- tutarli gorunsun.
	local fallback = defaultTheme()
	if fallback ~= nil then
		Decor.applyTheme(fallback.id)
	end
end

--- Temanin renklerini boyanmis parcalara uygular.
function Decor.applyTheme(themeId: string): boolean
	local theme = themeById[themeId]
	if theme == nil or themeId == currentTheme then
		return false
	end
	currentTheme = themeId

	local colors = {
		wall = Build.color(theme.wall),
		wallTrim = Build.color(theme.wallTrim),
		floor = Build.color(theme.floor),
		floorTile = Build.color(theme.floorTile),
		accent = Build.color(theme.accent),
	}
	for role, parts in painted do
		local color = colors[role]
		if color == nil then
			continue
		end
		for _, part in parts do
			part.Color = color
		end
	end
	return true
end

--- Sahip olunan sus esyalarini gorunur, digerlerini gorunmez yapar.
function Decor.applyProps(owned: { [string]: boolean })
	for id, model in propModels do
		local show = owned[id] == true
		for _, part in model:GetDescendants() do
			if part:IsA("BasePart") then
				-- Cam govde zaten yari saydamdi; gorunur yapinca onu
				-- tamamen opaklastirmamak icin ad denetimi.
				part.Transparency = if show then (if part.Name == "Cam" then 0.65 else 0) else 1
				part.CanCollide = show and part.Name ~= "Cam"
			end
		end
	end
end

--- Online oyuncularin sahip oldugu dekora gore klinigi tazeler.
--- `profiles` her oyuncunun `decor` tablosu.
function Decor.refresh(profiles: { { [string]: boolean } })
	local best = defaultTheme()
	local owned: { [string]: boolean } = {}

	for _, bag in profiles do
		for id, has in bag do
			if has ~= true then
				continue
			end
			local theme = themeById[id]
			if theme ~= nil then
				if best == nil or theme.order > best.order then
					best = theme
				end
			elseif propById[id] ~= nil then
				owned[id] = true
			end
		end
	end

	if best ~= nil then
		Decor.applyTheme(best.id)
	end
	Decor.applyProps(owned)
end

--- Su an uygulanan tema (SelfTest ve hata ayiklama icin).
function Decor.activeTheme(): string
	return currentTheme
end

--- Boyanan parca sayisi (SelfTest bunu olcuyor: sifirsa ad eslesmesi
--- bozulmus demektir).
function Decor.paintedCount(): number
	local total = 0
	for _, parts in painted do
		total += #parts
	end
	return total
end

--- Kurulmus sus esyasi sayisi.
function Decor.propCount(): number
	local total = 0
	for _ in propModels do
		total += 1
	end
	return total
end

return Decor
