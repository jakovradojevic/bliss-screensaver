from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

frame = Image.open(r"C:\Users\NinjaArt\Desktop\Bliss Screensaver\frame.png").convert("RGB")
side = frame.height
left = (frame.width - side) // 2
crop = frame.crop((left, 0, left + side, side))
master = crop.resize((1024, 1024), Image.Resampling.LANCZOS)
master = ImageEnhance.Color(master).enhance(1.12)
master = ImageEnhance.Contrast(master).enhance(1.06)
master = master.convert("RGBA")

radius = int(1024 * 0.22)
mask = Image.new("L", (1024, 1024), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, 1023, 1023), radius=radius, fill=255)
# Soften the cut so the corner does not look serrated when scaled down.
mask = mask.filter(ImageFilter.GaussianBlur(0.6))
master.putalpha(mask)

rim = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
draw = ImageDraw.Draw(rim)
draw.rounded_rectangle((8, 8, 1015, 1015), radius=radius - 6, outline=(255, 255, 255, 70), width=10)
master = Image.alpha_composite(master, rim)

out = r"C:\Users\NinjaArt\Desktop\Bliss Screensaver"
master.resize((256, 256), Image.Resampling.LANCZOS).save(out + r"\Bliss-icon.png")
sizes = [256, 128, 64, 48, 32, 24, 16]
icons = [master.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]
icons[0].save(
    out + r"\Bliss.ico",
    format="ICO",
    sizes=[(s, s) for s in sizes],
    append_images=icons[1:],
)
