from PIL import Image, ImageDraw
S=1024
BG=(0x17,0x19,0x1E,255); LINE=(0x2B,0x2F,0x37,255); OR=(0xFF,0x7A,0x1A,255); ORD=(0xC4,0x55,0x0A,255); GR=(0x38,0xD3,0x9F,255); BGD=(0x0F,0x10,0x13,255)
img=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(img)
d.rounded_rectangle([40,40,S-40,S-40],210,fill=BG,outline=LINE,width=14)
px,py=512,720  # pivot
# stick layer drawn upright, then tilted around the pivot
st=Image.new("RGBA",(S,S),(0,0,0,0)); s=ImageDraw.Draw(st)
s.rounded_rectangle([px-42,py-380,px+42,py+10],42,fill=OR)            # shaft
s.rounded_rectangle([px-105,py-560,px+105,py-250],105,fill=OR)        # grip
s.ellipse([px-46,py-520,px+46,py-428],fill=BGD)                        # hat
s.ellipse([px-26,py-500,px+26,py-448],fill=GR)
st=st.rotate(-12,center=(px,py),resample=Image.BICUBIC)
# base under the stick
d.rounded_rectangle([210,742,814,862],60,fill=ORD)
d.rounded_rectangle([210,712,814,822],55,fill=OR)
img.alpha_composite(st)
d=ImageDraw.Draw(img)
d.ellipse([px-90,py-38,px+90,py+38],fill=BGD)                          # boot ring over the base
d.ellipse([px-44,py-22,px+44,py+16],fill=ORD)
img.save("icon-1024.png")  # assets/icon.png
sizes=[16,20,24,32,40,48,64,128,256]
img.resize((256,256),Image.LANCZOS).save("JoystickKeeper.ico",sizes=[(z,z) for z in sizes])
prev=Image.new("RGBA",(900,320),(0x0F,0x10,0x13,255)); x=30
for z in [256,128,64,48,32,24,16]:
    t=img.resize((z,z),Image.LANCZOS); prev.alpha_composite(t,(x,(320-z)//2)); x+=z+30
prev.save("preview.png")
