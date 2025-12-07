from flask import Flask, request, jsonify
from flask_cors import CORS
import requests
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

app = Flask(__name__)
CORS(app)  # Enable CORS for frontend to connect

# Get API key from .env
SILICONFLOW_API_KEY = os.getenv("SILICONFLOW_API_KEY")

# System prompt
SYSTEM_PROMPT = """
Siz Bozorlik AI chatbot sizisiz. Sizning vazifangiz foydalanuvchilarga savollariga yordam berish.

MUHIM QOIDALAR:
1. Faqat O'ZBEK yoki RUS tilida javob bering – foydalanuvchi qaysi tilda yozsa, shu tilda javob bering.
2. Boshqa tillarda hech qachon javob bermang.
3. Javoblaringizni QISQA va TUSHUNARLI qiling.
4. Agar sizdan kim ekanligingizni so'rashsa: "Men Bozorlik AI chatbot man, sizga yordam berishga tayyorman" deb javob bering.
5. Har doim do'stona va samimiy bo'ling.

ВАЖНЫЕ ПРАВИЛА:
1. Отвечайте ТОЛЬКО на УЗБЕКСКОМ или РУССКОМ языке — на том языке, на котором задаёт пользователь.
2. Никогда не отвечайте на других языках.
3. Делайте ответы КОРОТКИМИ и ПОНЯТНЫМИ.
4. Если вас спросят кто вы, отвечайте: "Я Bozorlik AI чатбот, готов помочь вам".
5. Всегда будьте дружелюбным и приветливым.

QO'SHIMCHA VAZIFA – JAVOB BERISHI SHART BO'LGAN 2 SAVOL:
1. "Bozorlik AI nima qiladi?" — qisqa qilib asosiy funksiyalarni tushuntiring: ro'yxat tuzish, kategoriyalash, xarajatlarni saqlash.
2. "Bozorlik AI qanday ishlaydi?" — jarayonni qisqa tushuntiring: foydalanuvchi aytadi/yozadi → bot ro'yxat yaratadi → belgilash va narx qo'shish mumkin.

ДОПОЛНИТЕЛЬНЫЕ ВОПРОСЫ, НА КОТОРЫЕ БОТ ОБЯЗАН ОТВЕТИТЬ:
1. "Что делает Bozorlik AI?" — кратко объясните функции: создание списка, категории, учёт расходов.
2. "Как работает Bozorlik AI?" — кратко опишите процесс: пользователь говорит/пишет → бот формирует список → можно отмечать покупки и цены.
"""

@app.route('/chat', methods=['POST'])
def chat():
    try:
        data = request.json
        user_message = data.get('message', '')
        
        print(f"[DEBUG] Received message: {user_message}")
        
        if not user_message:
            return jsonify({'error': 'No message provided'}), 400
        
        # Call SiliconFlow API
        headers = {
            'Authorization': f'Bearer {SILICONFLOW_API_KEY}',
            'Content-Type': 'application/json'
        }
        
        payload = {
            'model': 'nex-agi/DeepSeek-V3.1-Nex-N1',
            'messages': [
                {
                    'role': 'system',
                    'content': SYSTEM_PROMPT
                },
                {
                    'role': 'user',
                    'content': user_message
                }
            ],
            'stream': False,
            'max_tokens': 512,
            'temperature': 0.7,
            'top_p': 0.9
        }
        
        print("[DEBUG] Sending request to SiliconFlow API...")
        print(f"[DEBUG] API URL: https://api.siliconflow.com/v1/chat/completions")
        print(f"[DEBUG] Model: {payload['model']}")
        
        response = requests.post(
            'https://api.siliconflow.com/v1/chat/completions',
            headers=headers,
            json=payload,
            timeout=30
        )
        
        print(f"[DEBUG] API Response Status: {response.status_code}")
        print(f"[DEBUG] API Response Text: {response.text[:500]}")  # First 500 chars
        
        if response.status_code == 200:
            result = response.json()
            bot_response = result['choices'][0]['message']['content']
            print(f"[DEBUG] Bot response: {bot_response[:100]}")
            return jsonify({'response': bot_response})
        else:
            error_text = response.text
            print(f"[ERROR] API Error Response: {error_text}")
            return jsonify({'error': f'API error: {response.status_code} - {error_text}'}), 500
            
    except requests.exceptions.RequestException as e:
        print(f"[ERROR] Request Error: {str(e)}")
        return jsonify({'error': f'API request failed: {str(e)}'}), 500
    except KeyError as e:
        print(f"[ERROR] KeyError: {str(e)}")
        return jsonify({'error': f'Invalid API response format: {str(e)}'}), 500
    except Exception as e:
        print(f"[ERROR] Unexpected Error: {str(e)}")
        import traceback
        traceback.print_exc()
        return jsonify({'error': f'Server error: {str(e)}'}), 500

@app.route('/health', methods=['GET'])
def health():
    return jsonify({'status': 'ok'})

if __name__ == '__main__':
    if not SILICONFLOW_API_KEY:
        print("WARNING: SILICONFLOW_API_KEY not found in .env file")
    else:
        print("SiliconFlow API key loaded successfully")
    
    print("Starting Bozorlik AI Chatbot Backend...")
    print("Server running on http://localhost:5000")
    app.run(host='0.0.0.0', port=5000, debug=True)
