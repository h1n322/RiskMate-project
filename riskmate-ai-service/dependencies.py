"""
dependencies.py — єдиний файл для Dependency Injection.

Принцип роботи:
  - @lru_cache гарантує, що YFinanceProvider створюється ОДИН РАЗ
    на весь час роботи сервера (singleton)
  - Кожен сервіс отримує провайдер через FastAPI Depends()
  - Роутери просто оголошують `service = Depends(get_simulation_service)`
    і FastAPI сам передає готовий об'єкт

Firebase:
  - firestore.client() викликається тут, а не всередині сервісу
  - UserService не знає як ініціалізується Firebase — це і є DI
"""
from functools import lru_cache

from fastapi import Depends
from firebase_admin import firestore

from infrastructure.data_provider import YFinanceProvider
from services.user_service import UserService
from services.predict_service import PredictService


# -----------------------------------------------------------------------
# Провайдер даних — singleton через lru_cache
# -----------------------------------------------------------------------

@lru_cache(maxsize=1)
def get_data_provider() -> YFinanceProvider:
    """
    Створюється один раз і живе весь час роботи сервера.
    lru_cache(maxsize=1) = singleton без додаткового коду.
    """
    return YFinanceProvider(retries=3, retry_delay=5.0)


# -----------------------------------------------------------------------
# Сервіси — створюються на кожен запит (FastAPI default scope)
# -----------------------------------------------------------------------



def get_predict_service(
    provider: YFinanceProvider = Depends(get_data_provider),
) -> PredictService:
    return PredictService(provider=provider)


def get_user_service() -> UserService:
    """
    Firestore client передається сюди — UserService не знає про Firebase.
    Firebase має бути ініціалізований у main.py до першого запиту.
    """
    db = firestore.client()
    return UserService(db_client=db)