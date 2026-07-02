import base64

def generate_assets():
    with open("bg.png", "rb") as image_file:
        encoded_string = base64.b64encode(image_file.read()).decode("utf-8")
        
    with open("assets.py", "w") as out_file:
        out_file.write(f'BG_BASE64 = "{encoded_string}"\n')
        
if __name__ == "__main__":
    generate_assets()
