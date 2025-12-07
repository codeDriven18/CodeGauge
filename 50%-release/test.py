from openai import OpenAI

client = OpenAI(
    api_key="sk-rpssftnkbhsnpdgwqbdkqyxawunqzsvegflogqnpmecivuwx",
    base_url="https://api.siliconflow.com/v1"
)

res = client.chat.completions.create(
    model="nex-agi/DeepSeek-V3.1-Nex-N1",
    messages=[{"role": "user", "content": "Hello"}]
)

print(res.choices[0].message.content)
