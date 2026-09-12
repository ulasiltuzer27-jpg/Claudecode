--!strict
-- Build.luau
-- ==========
-- Part kurma yardimcilari. Klinik, esyalar ve hayvanlar CALISMA ANINDA
-- buradan geciyor.
--
-- Neden XML yerine kod?
-- ---------------------
-- .rbxlx'te `Material` gibi enum'lar sayisal token olarak yazilmak zorunda
-- (`<token name="Material">272</token>`). Bu sayilari Studio olmadan
-- dogrulamak mumkun degil; yanlis token sessizce yanlis gorunum demek.
-- Burada ayni sey `Enum.Material.SmoothPlastic` diye yaziliyor: okunur,
-- diff'i anlamli, yanlissa Output'ta hata verir.

local Build = {}

--- [r,g,b] tablosunu Color3'e cevirir. Veri dosyalarindaki butun renkler
--- bu bicimde; tek cevrim noktasi burasi.
function Build.color(rgb: { number }): Color3
	return Color3.fromRGB(rgb[1], rgb[2], rgb[3])
end

--- Bir rengi aydinlatir (0..1). Kenar/aksan parcalari icin.
function Build.lighten(color: Color3, amount: number): Color3
	return Color3.new(
		color.R + (1 - color.R) * amount,
		color.G + (1 - color.G) * amount,
		color.B + (1 - color.B) * amount
	)
end

--- Bir rengi koyulastirir (0..1).
function Build.darken(color: Color3, amount: number): Color3
	return Color3.new(color.R * (1 - amount), color.G * (1 - amount), color.B * (1 - amount))
end

export type PartSpec = {
	name: string?,
	size: Vector3,
	cf: CFrame?,
	color: Color3?,
	material: Enum.Material?,
	shape: Enum.PartType?,
	transparency: number?,
	reflectance: number?,
	anchored: boolean?,
	canCollide: boolean?,
	canQuery: boolean?,
	castShadow: boolean?,
	parent: Instance?,
}

--- Tek bir Part. Yuzeyler HER ZAMAN Smooth: Instance.new varsayilani
--- eski "Studs" yuzeyi verebiliyor ve model bir anda 2008 gorunumune
--- dusuyor.
function Build.part(spec: PartSpec): Part
	local part = Instance.new("Part")
	part.Name = spec.name or "Part"
	part.Size = spec.size
	part.CFrame = spec.cf or CFrame.new()
	part.Color = spec.color or Color3.fromRGB(235, 235, 235)
	part.Material = spec.material or Enum.Material.SmoothPlastic
	part.Shape = spec.shape or Enum.PartType.Block
	part.Transparency = spec.transparency or 0
	part.Reflectance = spec.reflectance or 0
	part.Anchored = if spec.anchored == nil then true else spec.anchored
	part.CanCollide = if spec.canCollide == nil then true else spec.canCollide
	part.CanQuery = if spec.canQuery == nil then true else spec.canQuery
	part.CastShadow = if spec.castShadow == nil then true else spec.castShadow
	part.TopSurface = Enum.SurfaceType.Smooth
	part.BottomSurface = Enum.SurfaceType.Smooth
	part.LeftSurface = Enum.SurfaceType.Smooth
	part.RightSurface = Enum.SurfaceType.Smooth
	part.FrontSurface = Enum.SurfaceType.Smooth
	part.BackSurface = Enum.SurfaceType.Smooth
	part.Parent = spec.parent
	return part
end

--- Silindir. DIKKAT: Roblox'ta silindirin ekseni X'tir; dikey bir direk
--- icin CFrame'i Z ekseninde 90 derece dondurmek gerekir. Bu tuzagi her
--- cagri yerinde tekrar hatirlamak yerine `Build.pillar` kullanilmali.
function Build.cylinder(spec: PartSpec): Part
	spec.shape = Enum.PartType.Cylinder
	return Build.part(spec)
end

--- Dikey silindir direk. `size` = Vector3.new(cap, yukseklik, cap).
function Build.pillar(spec: PartSpec): Part
	local height = spec.size.Y
	local diameter = spec.size.X
	local base = spec.cf or CFrame.new()
	spec.size = Vector3.new(height, diameter, diameter)
	spec.cf = base * CFrame.Angles(0, 0, math.rad(90))
	spec.shape = Enum.PartType.Cylinder
	return Build.part(spec)
end

--- Kure.
function Build.ball(spec: PartSpec): Part
	spec.shape = Enum.PartType.Ball
	return Build.part(spec)
end

--- Kama. Ayri bir sinif (WedgePart); Part.Shape ile yapilmiyor.
function Build.wedge(spec: PartSpec): WedgePart
	local part = Instance.new("WedgePart")
	part.Name = spec.name or "Wedge"
	part.Size = spec.size
	part.CFrame = spec.cf or CFrame.new()
	part.Color = spec.color or Color3.fromRGB(235, 235, 235)
	part.Material = spec.material or Enum.Material.SmoothPlastic
	part.Transparency = spec.transparency or 0
	part.Anchored = if spec.anchored == nil then true else spec.anchored
	part.CanCollide = if spec.canCollide == nil then true else spec.canCollide
	part.TopSurface = Enum.SurfaceType.Smooth
	part.BottomSurface = Enum.SurfaceType.Smooth
	part.Parent = spec.parent
	return part
end

--- Model kabugu.
function Build.model(name: string, parent: Instance?): Model
	local model = Instance.new("Model")
	model.Name = name
	model.Parent = parent
	return model
end

--- Bir modelin butun parcalarini kok parcaya kaynaklar ve kok disindaki
--- her seyi Anchored=false yapar. Hareketli modeller (hayvanlar) icin.
function Build.weldAll(model: Model, root: BasePart)
	for _, descendant in model:GetDescendants() do
		if descendant:IsA("BasePart") and descendant ~= root then
			local weld = Instance.new("WeldConstraint")
			weld.Part0 = root
			weld.Part1 = descendant
			weld.Parent = root
			descendant.Anchored = false
			descendant.CanCollide = false
		end
	end
end

--- Bir yuzeye yazi. SurfaceGui + TextLabel; tabela ve monitor icin.
function Build.surfaceText(
	part: BasePart,
	face: Enum.NormalId,
	text: string,
	color: Color3,
	scaled: boolean?
): TextLabel
	local gui = Instance.new("SurfaceGui")
	gui.Face = face
	gui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	gui.PixelsPerStud = 50
	gui.AlwaysOnTop = false
	gui.Parent = part

	local label = Instance.new("TextLabel")
	label.Size = UDim2.fromScale(1, 1)
	label.BackgroundTransparency = 1
	label.Text = text
	label.TextColor3 = color
	label.Font = Enum.Font.GothamBold
	label.TextScaled = if scaled == nil then true else scaled
	label.TextWrapped = true
	label.Parent = gui
	return label
end

--- Tavan/oda isigi. Govde + PointLight.
function Build.ceilingLight(position: Vector3, parent: Instance, brightness: number?): Part
	local housing = Build.part({
		name = "Lamba",
		size = Vector3.new(6, 0.35, 2.2),
		cf = CFrame.new(position),
		color = Color3.fromRGB(248, 250, 252),
		material = Enum.Material.Neon,
		canCollide = false,
		parent = parent,
	})
	local light = Instance.new("PointLight")
	light.Brightness = brightness or 1.6
	light.Range = 26
	light.Color = Color3.fromRGB(255, 248, 232)
	light.Shadows = true
	light.Parent = housing
	return housing
end

--- Yatay dikdortgen zemin/tavan levhasi. min/max [x,z] cinsinden.
function Build.slab(
	minXZ: { number },
	maxXZ: { number },
	y: number,
	thickness: number,
	color: Color3,
	material: Enum.Material,
	parent: Instance,
	name: string
): Part
	local width = maxXZ[1] - minXZ[1]
	local depth = maxXZ[2] - minXZ[2]
	local centerX = (minXZ[1] + maxXZ[1]) * 0.5
	local centerZ = (minXZ[2] + maxXZ[2]) * 0.5
	return Build.part({
		name = name,
		size = Vector3.new(width, thickness, depth),
		cf = CFrame.new(centerX, y, centerZ),
		color = color,
		material = material,
		parent = parent,
	})
end

return Build
