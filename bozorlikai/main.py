import openai
import aiohttp
import logging
import json
import os
from typing import Dict, List, Tuple, Optional
from datetime import datetime
from aiogram import Bot, Dispatcher, types
from aiogram.utils import executor
from aiogram.types import ContentType, InlineKeyboardMarkup, InlineKeyboardButton
from aiogram.types import ReplyKeyboardRemove

import os
from dotenv import load_dotenv

logging.basicConfig(level=logging.INFO)

TOKEN = os.getenv("TOKEN")
load_dotenv()
api_key = os.getenv("OPENAI_API_KEY")

bot = Bot(token=TOKEN)
dp = Dispatcher(bot)
openai.api_key = OPENAI_API_KEY

# Хранилище данных пользователей
user_data: Dict[int, Dict] = {}

# Файл для хранения аналитики расходов
EXPENSES_FILE = "shopping_expenses.json"

SYSTEM_PROMPT = """
You are Bozorlik AI — an assistant that ONLY creates grocery shopping lists.
You MUST always respond in Russian.

GENERAL RULES:
1) Always respond in Russian.
2) You ONLY help with grocery shopping lists.
3) If the user asks anything unrelated to groceries (math, homework, theory questions), answer:
   "Извините, я могу помочь только со списком базара."
4) If the user greets you ("привет", "салам", "здравствуйте"), reply:
   "Привет! Что нужно купить сегодня?"

MAIN LIST RULE:
When the user provides a grocery list, you MUST immediately format it into a categorized shopping list.

CATEGORY FORMAT RULES (IMPORTANT):
• NEVER write the word "Категория".
• The format MUST be:

     🥕 Овощи:
     • Лук — 1 кг
     • Морковь — 2 кг

     🥛 Молочные продукты:
     • Молоко — 1 литр

• Only category name + emoji + colon.
• Do NOT modify this format.
• Use ONLY bullet points (•) for items, NEVER dashes (-).

CATEGORY RULES:
• Create ONLY categories that contain items.
• Never create empty categories.
• Never invent items that the user did not mention.
• Allowed categories (use ONLY these):
     🥕 Овощи
     🍎 Фрукты
     🥛 Молочные продукты
     🍖 Мясо и рыба
     📦 Бакалея
     🥤 Напитки
     🧴 Химия
     📝 Другое
• You may fix small spelling mistakes but do NOT change product meaning.

ITEM FORMAT RULES:
• Format every product as:
     • Название — количество
• If the user did not specify quantity, leave it empty:
     • Яблоко —
• ALWAYS use bullet points (•) NOT dashes (-)

FINAL RULES:
• NO explanations.
• NO English in answers.
• NO commentary.
• ONLY the formatted grocery list OR the short greeting/refusal message.
• NEVER use dashes (-) for items, ALWAYS use bullet points (•)

Process the user input:
"""

SYSTEM_PROMPT_PURCHASE = """
Ты — AI помощник для определения покупок из списка. Твоя задача — определить какие продукты были куплены из сообщения пользователя и их стоимость.

ПРАВИЛА:
1. Отвечай ТОЛЬКО в формате JSON
2. Формат ответа: {"products": [{"name": "продукт1", "price": 10000}, {"name": "продукт2", "price": 5000}]}
3. Если не можешь определить продукты, возвращай: {"products": []}
4. Распознавай синонимы (например: "купил", "приобрел", "взял", "купили", "купила")
5. Распознавай продукты в разных падежах
6. Распознавай стоимость в разных форматах: "20 тысяч", "20.000 сум", "20000 сум", "20 тыс"
7. Игнорируй все, что не является продуктом из списка

Примеры:
- Пользователь: "купил огурцы за 15 тысяч и помидоры за 20.000 сум" → {"products": [{"name": "огурцы", "price": 15000}, {"name": "помидоры", "price": 20000}]}
- Пользователь: "приобрел молоко за 12.000 сум" → {"products": [{"name": "молоко", "price": 12000}]}
- Пользователь: "взял хлеб за 5 тысяч и сыр за 25.000" → {"products": [{"name": "хлеб", "price": 5000}, {"name": "сыр", "price": 25000}]}
- Пользователь: "сегодня хорошая погода" → {"products": []}

Определи продукты и их стоимость из сообщения:
"""

SYSTEM_PROMPT_EDIT = """
Ты — AI помощник для редактирования списка покупок. Твоя задача — понять, что пользователь хочет изменить в существующем списке.

ПРАВИЛА:
1. Отвечай ТОЛЬКО в формате JSON
2. Формат ответа: {"changes": [{"action": "add/remove/replace", "old_product": "старый продукт", "new_product": "новый продукт", "quantity": "количество"}]}
3. Если не можешь определить изменения, возвращай: {"changes": []}
4. Распознавай команды:
   - "добавь", "добавить", "хочу добавить" → action: "add"
   - "удали", "убрать", "убери", "не нужно" → action: "remove" 
   - "замени", "измени", "поменяй" → action: "replace"
5. Распознавай продукты и количества

Примеры:
- Пользователь: "добавь молоко 1 литр" → {"changes": [{"action": "add", "old_product": "", "new_product": "молоко", "quantity": "1 литр"}]}
- Пользователь: "удали картошку" → {"changes": [{"action": "remove", "old_product": "картошка", "new_product": "", "quantity": ""}]}
- Пользователь: "замени картошку 2 кг на лук 1 кг" → {"changes": [{"action": "replace", "old_product": "картошка", "new_product": "лук", "quantity": "1 кг"}]}
- Пользователь: "привет" → {"changes": []}

Определи изменения из сообщения:
"""


def load_expenses():
    """Загружает данные о расходах из файла"""
    if os.path.exists(EXPENSES_FILE):
        try:
            with open(EXPENSES_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            logging.error(f"Error loading expenses: {e}")
            return {}
    return {}


def save_expenses(expenses_data):
    """Сохраняет данные о расходах в файл"""
    try:
        with open(EXPENSES_FILE, 'w', encoding='utf-8') as f:
            json.dump(expenses_data, f, ensure_ascii=False, indent=2)
    except Exception as e:
        logging.error(f"Error saving expenses: {e}")


def parse_shopping_list(text: str) -> Dict[str, List[Tuple[str, str]]]:
    """Парсит отформатированный список покупок на категории и товары"""
    categories = {}
    current_category = None

    lines = text.split('\n')
    for line in lines:
        line = line.strip()
        if not line:
            continue

        # Определяем категорию (строка с эмодзи и двоеточием)
        if any(emoji in line for emoji in ['🥕', '🍎', '🥛', '🍖', '📦', '🥤', '🧴', '📝']) and line.endswith(':'):
            current_category = line[:-1]
            categories[current_category] = []
        elif (line.startswith('•') or line.startswith('-')) and current_category:
            if line.startswith('-'):
                line = '•' + line[1:]

            product_line = line[1:].strip()
            if '—' in product_line:
                product, quantity = product_line.split('—', 1)
                product = product.strip()
                quantity = quantity.strip()
            elif '-' in product_line:
                product, quantity = product_line.split('-', 1)
                product = product.strip()
                quantity = quantity.strip()
            else:
                product = product_line
                quantity = ""
            categories[current_category].append((product, quantity, False, 0))  # False - не куплен, 0 - цена

    return categories


def format_shopping_list(categories: Dict[str, List[Tuple[str, str, bool, int]]]) -> str:
    result = []

    for category, items in categories.items():
        if items:  # Только непустые категории
            result.append(f"{category}:")
            for product, quantity, purchased, price in items:
                if purchased and price > 0:
                    result.append(f"✅ {product} — {quantity} - {price:,} сум".replace(',', '.'))
                elif purchased:
                    result.append(f"✅ {product} — {quantity}")
                else:
                    result.append(f"• {product} — {quantity}")
            result.append("")  # Пустая строка между категориями

    return "\n".join(result).strip()


async def format_list_with_gpt(text: str) -> str:
    completion = openai.chat.completions.create(
        model="gpt-4o-mini-2024-07-18",
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": text}
        ]
    )
    return completion.choices[0].message.content


async def detect_purchased_products_with_prices(text: str, available_products: List[str]) -> List[Dict]:
    prompt = f"""
Доступные продукты: {', '.join(available_products)}

Сообщение пользователя: "{text}"

Определи какие продукты из доступных были куплены и их стоимость в сумах. Верни ТОЛЬКО JSON:
"""

    try:
        completion = openai.chat.completions.create(
            model="gpt-4o-mini-2024-07-18",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT_PURCHASE},
                {"role": "user", "content": prompt}
            ]
        )

        response = completion.choices[0].message.content
        data = json.loads(response)
        return data.get("products", [])
    except Exception as e:
        logging.error(f"Error detecting purchased products with prices: {e}")
        return []


async def detect_edit_changes(text: str) -> List[Dict]:
    """Определяет изменения для редактирования списка"""
    try:
        completion = openai.chat.completions.create(
            model="gpt-4o-mini-2024-07-18",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT_EDIT},
                {"role": "user", "content": text}
            ]
        )

        response = completion.choices[0].message.content
        data = json.loads(response)
        return data.get("changes", [])
    except Exception as e:
        logging.error(f"Error detecting edit changes: {e}")
        return []


async def transcribe_voice(file_path: str) -> str:
    with open(file_path, "rb") as audio_file:
        transcript = openai.audio.transcriptions.create(
            model="whisper-1",
            file=audio_file
        )
    return transcript.text


def get_all_products_from_categories(categories: Dict[str, List[Tuple[str, str, bool, int]]]) -> List[str]:
    all_products = []
    for category_items in categories.values():
        for product, _, _, _ in category_items:
            all_products.append(product.lower())
    return all_products


def mark_products_as_purchased_with_prices(categories: Dict[str, List[Tuple[str, str, bool, int]]],
                                           purchased_products: List[Dict]) -> Tuple[
    Dict[str, List[Tuple[str, str, bool, int]]], int]:
    total_cost = 0
    purchased_products_lower = {p['name'].lower(): p.get('price', 0) for p in purchased_products}

    updated_categories = {}
    for category, items in categories.items():
        updated_items = []
        for product, quantity, purchased, current_price in items:
            product_lower = product.lower()
            # Проверяем совпадение продукта
            is_purchased = purchased
            price = current_price

            for purchased_product, purchased_price in purchased_products_lower.items():
                if (purchased_product in product_lower or product_lower in purchased_product) and not purchased:
                    is_purchased = True
                    price = purchased_price
                    total_cost += purchased_price
                    break

            updated_items.append((product, quantity, is_purchased, price))
        updated_categories[category] = updated_items

    return updated_categories, total_cost


def is_purchase_message(text: str) -> bool:
    purchase_keywords = ['купил', 'купила', 'купили', 'приобрел', 'приобрела', 'приобрели',
                         'взял', 'взяла', 'взяли', 'куплено', 'приобретено', 'купилa', 'за', 'сум']
    text_lower = text.lower()
    return any(keyword in text_lower for keyword in purchase_keywords)


def calculate_completion_percentage(categories: Dict[str, List[Tuple[str, str, bool, int]]]) -> Tuple[int, int, int]:
    total_items = 0
    purchased_items = 0
    total_cost = 0

    for items in categories.values():
        for _, _, purchased, price in items:
            total_items += 1
            if purchased:
                purchased_items += 1
                total_cost += price

    if total_items == 0:
        return 0, 0, 0

    percentage = (purchased_items / total_items) * 100
    return int(percentage), purchased_items, total_cost


def fix_list_formatting(text: str) -> str:
    lines = text.split('\n')
    fixed_lines = []

    for line in lines:
        line = line.strip()
        if not line:
            fixed_lines.append("")
            continue

        # Исправляем категории (добавляем эмодзи если нужно)
        if line.lower().startswith('овощи') and line.endswith(':'):
            fixed_lines.append("🥕 Овощи:")
        elif line.lower().startswith('фрукты') and line.endswith(':'):
            fixed_lines.append("🍎 Фрукты:")
        elif any(word in line.lower() for word in ['молочные', 'молоко']) and line.endswith(':'):
            fixed_lines.append("🥛 Молочные продукты:")
        elif any(word in line.lower() for word in ['мясо', 'рыба']) and line.endswith(':'):
            fixed_lines.append("🍖 Мясо и рыба:")
        elif line.lower().startswith('бакалея') and line.endswith(':'):
            fixed_lines.append("📦 Бакалея:")
        elif line.lower().startswith('напитки') and line.endswith(':'):
            fixed_lines.append("🥤 Напитки:")
        elif line.lower().startswith('химия') and line.endswith(':'):
            fixed_lines.append("🧴 Химия:")
        elif line.lower().startswith('другое') and line.endswith(':'):
            fixed_lines.append("📝 Другое:")
        elif line.startswith('-'):
            fixed_line = '•' + line[1:]
            fixed_lines.append(fixed_line)
        else:
            fixed_lines.append(line)

    return '\n'.join(fixed_lines)


def save_shopping_history(user_id: int, categories: Dict[str, List[Tuple[str, str, bool, int]]], total_cost: int):
    expenses_data = load_expenses()

    if str(user_id) not in expenses_data:
        expenses_data[str(user_id)] = []

    purchase_record = {
        "date": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "total_cost": total_cost,
        "items": []
    }

    for category, items in categories.items():
        for product, quantity, purchased, price in items:
            if purchased and price > 0:
                purchase_record["items"].append({
                    "product": product,
                    "quantity": quantity,
                    "category": category,
                    "price": price
                })

    expenses_data[str(user_id)].append(purchase_record)
    save_expenses(expenses_data)


def get_total_expenses(user_id: int) -> int:
    expenses_data = load_expenses()
    user_expenses = expenses_data.get(str(user_id), [])
    total = sum(record["total_cost"] for record in user_expenses)
    return total


def apply_edit_changes(categories: Dict[str, List[Tuple[str, str, bool, int]]], changes: List[Dict]) -> Dict[
    str, List[Tuple[str, str, bool, int]]]:
    """Применяет изменения к списку покупок"""
    updated_categories = {category: items.copy() for category, items in categories.items()}

    for change in changes:
        action = change.get("action")
        old_product = change.get("old_product", "").lower()
        new_product = change.get("new_product", "")
        quantity = change.get("quantity", "")

        if action == "remove":
            # Удаляем продукт из всех категорий
            for category, items in updated_categories.items():
                updated_categories[category] = [
                    item for item in items
                    if item[0].lower() != old_product
                ]

        elif action == "add":
            # Добавляем продукт в категорию "Другое" или первую доступную категорию
            if "📝 Другое" in updated_categories:
                updated_categories["📝 Другое"].append((new_product, quantity, False, 0))
            else:
                # Если категории "Другое" нет, создаем ее
                updated_categories["📝 Другое"] = [(new_product, quantity, False, 0)]

        elif action == "replace":
            # Заменяем старый продукт на новый
            for category, items in updated_categories.items():
                for i, (product, qty, purchased, price) in enumerate(items):
                    if product.lower() == old_product:
                        updated_categories[category][i] = (new_product, quantity, purchased, price)

    # Удаляем пустые категории
    updated_categories = {category: items for category, items in updated_categories.items() if items}

    return updated_categories


def create_list_keyboard() -> InlineKeyboardMarkup:
    """Создает клавиатуру с кнопками редактирования и очистки"""
    keyboard = InlineKeyboardMarkup(row_width=2)
    keyboard.add(
        InlineKeyboardButton("✏️ Редактировать", callback_data="edit_list"),
        InlineKeyboardButton("🗑 Очистить", callback_data="clear_list")
    )
    return keyboard


@dp.message_handler(commands=['start'])
async def start_handler(message: types.Message):
    await message.reply(
        "Привет! 😊 Я помогу тебе составить список базара и отслеживать расходы. Отправь текст или голосовое сообщение с тем, что нужно купить.\n\nКоманды:\n/list - показать текущий список\n/clear - очистить список\n/status - показать прогресс покупок\n/expenses - показать историю расходов\n/total - общие расходы за все время")


@dp.message_handler(commands=['clear'])
async def clear_handler(message: types.Message):
    user_id = message.from_user.id
    if user_id in user_data:
        # Удаляем сообщение со списком если оно есть
        if 'list_message_id' in user_data[user_id]:
            try:
                await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
            except Exception as e:
                logging.error(f"Error deleting message: {e}")
        del user_data[user_id]

    keyboard = InlineKeyboardMarkup()
    keyboard.add(InlineKeyboardButton("📝 Написать новый список", callback_data="new_list"))

    await message.reply("🗑 Список покупок очищен! Хотите написать новый?", reply_markup=keyboard)


@dp.message_handler(commands=['list'])
async def list_handler(message: types.Message):
    user_id = message.from_user.id
    if user_id in user_data and user_data[user_id].get('categories'):
        categories = user_data[user_id]['categories']
        formatted_list = format_shopping_list(categories)
        percentage, purchased_count, total_cost = calculate_completion_percentage(categories)

        response = f"🛒 Твой текущий список:\n\n{formatted_list}"
        if percentage > 0:
            response += f"\n\n📊 Прогресс: {percentage}% ({purchased_count} товаров куплено)"
            if total_cost > 0:
                response += f"\n💰 Потрачено: {total_cost:,} сум".replace(',', '.')

        sent_message = await message.reply(response, reply_markup=create_list_keyboard())
        # Сохраняем ID сообщения со списком
        user_data[user_id]['list_message_id'] = sent_message.message_id
    else:
        await message.reply("📝 У тебя еще нет списка покупок. Напиши что нужно купить!")


@dp.message_handler(commands=['status'])
async def status_handler(message: types.Message):
    user_id = message.from_user.id
    if user_id in user_data and user_data[user_id].get('categories'):
        categories = user_data[user_id]['categories']
        percentage, purchased_count, total_cost = calculate_completion_percentage(categories)
        total_items = sum(len(items) for items in categories.values())

        if percentage == 100:
            response = f"🎉 Поздравляю! Все {total_items} товаров куплены! Список завершен!"
            if total_cost > 0:
                response += f"\n💰 Общая стоимость: {total_cost:,} сум".replace(',', '.')
            await message.reply(response)
        else:
            progress_bar = "🟩" * (percentage // 10) + "⬜" * (10 - percentage // 10)
            response = f"📊 Прогресс покупок:\n\n{progress_bar} {percentage}%\n\n✅ Куплено: {purchased_count}/{total_items} товаров"
            if total_cost > 0:
                response += f"\n💰 Потрачено: {total_cost:,} сум".replace(',', '.')
            await message.reply(response)
    else:
        await message.reply("📝 У тебя еще нет списка покупок. Напиши что нужно купить!")


@dp.message_handler(commands=['expenses'])
async def expenses_handler(message: types.Message):
    user_id = message.from_user.id
    expenses_data = load_expenses()
    user_expenses = expenses_data.get(str(user_id), [])

    if not user_expenses:
        await message.reply("📊 У тебя еще нет истории расходов.")
        return

    response = "📊 История твоих покупок:\n\n"
    for i, record in enumerate(user_expenses[-5:], 1):  # последние 5 записей
        response += f"{i}. {record['date']}\n"
        response += f"   💰 Общая сумма: {record['total_cost']:,} сум\n".replace(',', '.')
        for item in record['items'][:3]:  # первые 3 товара
            response += f"   • {item['product']} - {item['price']:,} сум\n".replace(',', '.')
        if len(record['items']) > 3:
            response += f"   ... и еще {len(record['items']) - 3} товаров\n"
        response += "\n"

    await message.reply(response)


@dp.message_handler(commands=['total'])
async def total_handler(message: types.Message):
    user_id = message.from_user.id
    total_expenses = get_total_expenses(user_id)

    if total_expenses > 0:
        await message.reply(f"💰 Твои общие расходы за все время: {total_expenses:,} сум".replace(',', '.'))
    else:
        await message.reply("📊 У тебя еще нет записей о расходах.")


@dp.callback_query_handler(lambda c: c.data == "edit_list")
async def process_edit_callback(callback_query: types.CallbackQuery):
    user_id = callback_query.from_user.id

    if user_id in user_data and user_data[user_id].get('categories'):
        # Устанавливаем режим редактирования
        user_data[user_id]['editing'] = True

        # Отправляем сообщение с инструкцией
        await bot.answer_callback_query(callback_query.id)
        await bot.send_message(
            user_id,
            "✏️ Режим редактирования включен. Отправь текст или голосовое сообщение с изменениями:\n\n"
            "• 'добавь [продукт] [количество]' - добавить продукт\n"
            "• 'удали [продукт]' - удалить продукт\n"
            "• 'замени [старый продукт] на [новый продукт]' - заменить продукт\n\n"
            f"Текущий список:\n{format_shopping_list(user_data[user_id]['categories'])}"
        )
    else:
        await bot.answer_callback_query(callback_query.id, "У тебя нет списка для редактирования")


@dp.callback_query_handler(lambda c: c.data == "clear_list")
async def process_clear_callback(callback_query: types.CallbackQuery):
    user_id = callback_query.from_user.id

    if user_id in user_data:
        # Удаляем сообщение со списком если оно есть
        if 'list_message_id' in user_data[user_id]:
            try:
                await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
            except Exception as e:
                logging.error(f"Error deleting message: {e}")
        del user_data[user_id]

    await bot.answer_callback_query(callback_query.id, "Список очищен")

    keyboard = InlineKeyboardMarkup()
    keyboard.add(InlineKeyboardButton("📝 Написать новый список", callback_data="new_list"))

    await bot.send_message(user_id, "🗑 Список покупок очищен! Хотите написать новый?", reply_markup=keyboard)


@dp.callback_query_handler(lambda c: c.data == "new_list")
async def process_new_list_callback(callback_query: types.CallbackQuery):
    await bot.answer_callback_query(callback_query.id)
    await bot.send_message(callback_query.from_user.id,
                           "📝 Отлично! Напиши или запиши голосовое сообщение с тем, что нужно купить:")


@dp.message_handler(content_types=ContentType.TEXT)
async def handle_text(message: types.Message):
    user_id = message.from_user.id
    text = message.text

    # Проверяем режим редактирования
    if user_id in user_data and user_data[user_id].get('editing'):
        categories = user_data[user_id]['categories']

        # Определяем изменения
        changes = await detect_edit_changes(text)

        if changes:
            # Применяем изменения
            updated_categories = apply_edit_changes(categories, changes)
            user_data[user_id]['categories'] = updated_categories
            user_data[user_id]['editing'] = False

            # Удаляем старое сообщение со списком
            if 'list_message_id' in user_data[user_id]:
                try:
                    await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                except Exception as e:
                    logging.error(f"Error deleting message: {e}")

            # Отправляем обновленный список
            formatted_list = format_shopping_list(updated_categories)
            total_items = sum(len(items) for items in updated_categories.values())

            response = f"✅ Список обновлен! ({total_items} товаров):\n\n{formatted_list}"
            sent_message = await message.reply(response, reply_markup=create_list_keyboard())
            user_data[user_id]['list_message_id'] = sent_message.message_id
        else:
            await message.reply(
                "❌ Не понял, что нужно изменить. Попробуй еще раз:\n\n• 'добавь молоко 1 литр'\n• 'удали картошку'\n• 'замени яблоки на груши'")

        return

    if user_id in user_data and user_data[user_id].get('categories') and is_purchase_message(text):
        categories = user_data[user_id]['categories']
        all_products = get_all_products_from_categories(categories)

        if all_products:
            purchased_products = await detect_purchased_products_with_prices(text, all_products)

            if purchased_products:
                updated_categories, new_costs = mark_products_as_purchased_with_prices(categories, purchased_products)
                user_data[user_id]['categories'] = updated_categories

                formatted_list = format_shopping_list(updated_categories)

                percentage, purchased_count, total_cost = calculate_completion_percentage(updated_categories)
                total_items = sum(len(items) for items in updated_categories.values())

                if percentage == 100:
                    save_shopping_history(user_id, updated_categories, total_cost)

                    response = f"🎉 Отлично! Все {total_items} товаров куплены! Список завершен!\n\n{formatted_list}\n\n💰 Общая стоимость покупки: {total_cost:,} сум".replace(
                        ',', '.')

                    # Удаляем старое сообщение со списком
                    if 'list_message_id' in user_data[user_id]:
                        try:
                            await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                        except Exception as e:
                            logging.error(f"Error deleting message: {e}")

                    await message.reply(response)
                    del user_data[user_id]
                else:
                    response = f"✅ Обновил список! Отметил купленное:\n\n{formatted_list}\n\n📊 Прогресс: {percentage}% ({purchased_count}/{total_items} товаров)"
                    if new_costs > 0:
                        response += f"\n💰 Добавлено расходов: {new_costs:,} сум".replace(',', '.')
                        response += f"\n💰 Всего потрачено: {total_cost:,} сум".replace(',', '.')

                    # Удаляем старое сообщение со списком
                    if 'list_message_id' in user_data[user_id]:
                        try:
                            await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                        except Exception as e:
                            logging.error(f"Error deleting message: {e}")

                    sent_message = await message.reply(response, reply_markup=create_list_keyboard())
                    user_data[user_id]['list_message_id'] = sent_message.message_id
            else:
                await message.reply(
                    "🤔 Не смог определить какие товары ты купил. Попробуй назвать их точнее, например: 'купил молоко за 12.000 сум и хлеб за 5 тысяч'")
        else:
            await message.reply("📝 Сначала создай список покупок!")

    else:
        response = await format_list_with_gpt(text)

        if any(emoji in response for emoji in ['🥕', '🍎', '🥛', '🍖', '📦', '🥤', '🧴', '📝']) or any(
                word in response.lower() for word in
                ['овощи:', 'фрукты:', 'молочные:', 'мясо:', 'бакалея:', 'напитки:', 'химия:', 'другое:']):
            response = fix_list_formatting(response)

            categories = parse_shopping_list(response)
            user_data[user_id] = {
                'categories': categories,
                'last_message_id': message.message_id,
                'editing': False
            }

            total_items = sum(len(items) for items in categories.values())
            response_with_info = f"📋 Создал список покупок ({total_items} товаров):\n\n{response}"

            sent_message = await message.reply(response_with_info, reply_markup=create_list_keyboard())
            user_data[user_id]['list_message_id'] = sent_message.message_id
        else:
            await message.reply(response)


@dp.message_handler(content_types=ContentType.VOICE)
async def handle_voice(message: types.Message):
    user_id = message.from_user.id

    # Проверяем режим редактирования
    if user_id in user_data and user_data[user_id].get('editing'):
        file_info = await bot.get_file(message.voice.file_id)
        file_url = f"https://api.telegram.org/file/bot{TOKEN}/{file_info.file_path}"

        async with aiohttp.ClientSession() as session:
            async with session.get(file_url) as resp:
                if resp.status == 200:
                    with open("voice_edit.ogg", "wb") as f:
                        f.write(await resp.read())

        text = await transcribe_voice("voice_edit.ogg")
        categories = user_data[user_id]['categories']

        # Определяем изменения
        changes = await detect_edit_changes(text)

        if changes:
            # Применяем изменения
            updated_categories = apply_edit_changes(categories, changes)
            user_data[user_id]['categories'] = updated_categories
            user_data[user_id]['editing'] = False

            # Удаляем старое сообщение со списком
            if 'list_message_id' in user_data[user_id]:
                try:
                    await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                except Exception as e:
                    logging.error(f"Error deleting message: {e}")

            # Отправляем обновленный список
            formatted_list = format_shopping_list(updated_categories)
            total_items = sum(len(items) for items in updated_categories.values())

            response = f"✅ Список обновлен! ({total_items} товаров):\n\n{formatted_list}"
            sent_message = await message.reply(response, reply_markup=create_list_keyboard())
            user_data[user_id]['list_message_id'] = sent_message.message_id
        else:
            await message.reply(
                "❌ Не понял, что нужно изменить. Попробуй сказать четче:\n\n• 'добавь молоко один литр'\n• 'удали картошку'\n• 'замени яблоки на груши'")

        return

    file_info = await bot.get_file(message.voice.file_id)
    file_url = f"https://api.telegram.org/file/bot{TOKEN}/{file_info.file_path}"

    async with aiohttp.ClientSession() as session:
        async with session.get(file_url) as resp:
            if resp.status == 200:
                with open("voice.ogg", "wb") as f:
                    f.write(await resp.read())

    text = await transcribe_voice("voice.ogg")

    if user_id in user_data and user_data[user_id].get('categories') and is_purchase_message(text):
        categories = user_data[user_id]['categories']
        all_products = get_all_products_from_categories(categories)

        if all_products:
            purchased_products = await detect_purchased_products_with_prices(text, all_products)

            if purchased_products:
                updated_categories, new_costs = mark_products_as_purchased_with_prices(categories, purchased_products)
                user_data[user_id]['categories'] = updated_categories

                formatted_list = format_shopping_list(updated_categories)

                percentage, purchased_count, total_cost = calculate_completion_percentage(updated_categories)
                total_items = sum(len(items) for items in updated_categories.values())

                if percentage == 100:
                    save_shopping_history(user_id, updated_categories, total_cost)

                    response = f"🎉 Отлично! Все {total_items} товаров куплены! Список завершен!\n\n{formatted_list}\n\n💰 Общая стоимость покупки: {total_cost:,} сум".replace(
                        ',', '.')

                    # Удаляем старое сообщение со списком
                    if 'list_message_id' in user_data[user_id]:
                        try:
                            await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                        except Exception as e:
                            logging.error(f"Error deleting message: {e}")

                    await message.reply(response)
                    del user_data[user_id]
                else:
                    response = f"✅ Обновил список! Отметил купленное:\n\n{formatted_list}\n\n📊 Прогресс: {percentage}% ({purchased_count}/{total_items} товаров)"
                    if new_costs > 0:
                        response += f"\n💰 Добавлено расходов: {new_costs:,} сум".replace(',', '.')
                        response += f"\n💰 Всего потрачено: {total_cost:,} сум".replace(',', '.')

                    # Удаляем старое сообщение со списком
                    if 'list_message_id' in user_data[user_id]:
                        try:
                            await bot.delete_message(user_id, user_data[user_id]['list_message_id'])
                        except Exception as e:
                            logging.error(f"Error deleting message: {e}")

                    sent_message = await message.reply(response, reply_markup=create_list_keyboard())
                    user_data[user_id]['list_message_id'] = sent_message.message_id
            else:
                await message.reply("🤔 Не смог определить какие товары ты купил. Попробуй назвать их точнее.")
        else:
            await message.reply("📝 Сначала создай список покупок!")
    else:
        response = await format_list_with_gpt(text)

        if any(emoji in response for emoji in ['🥕', '🍎', '🥛', '🍖', '📦', '🥤', '🧴', '📝']) or any(
                word in response.lower() for word in
                ['овощи:', 'фрукты:', 'молочные:', 'мясо:', 'бакалея:', 'напитки:', 'химия:', 'другое:']):
            response = fix_list_formatting(response)

            categories = parse_shopping_list(response)
            user_data[user_id] = {
                'categories': categories,
                'last_message_id': message.message_id,
                'editing': False
            }

            total_items = sum(len(items) for items in categories.values())
            response_with_info = f"📋 Создал список покупок ({total_items} товаров):\n\n{response}"

            sent_message = await message.reply(response_with_info, reply_markup=create_list_keyboard())
            user_data[user_id]['list_message_id'] = sent_message.message_id
        else:
            await message.reply(response)


if __name__ == "__main__":
    executor.start_polling(dp, skip_updates=True)
