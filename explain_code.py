import openai

openai.api_key = "sk-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"

response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[
        {"role": "system", "content": "Du er en hjælpsom AI der forklarer kode."},
        {"role": "user", "content": "Forklar denne funktion:\ndef greet(name):\n    return f'Hello, {name}!'"},
    ]
)

print(response['choices'][0]['message']['content'])
