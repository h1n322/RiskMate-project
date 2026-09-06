import os
from dotenv import load_dotenv
import stripe
import firebase_admin
from firebase_admin import credentials
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from api.routes_market import router as market_router
from api.routes_billing import router as billing_router

import json

load_dotenv()

firebase_creds_env = os.environ.get("FIREBASE_CREDENTIALS")
if firebase_creds_env:
    cred = credentials.Certificate(json.loads(firebase_creds_env))
else:
    cred = credentials.Certificate("serviceAccountKey.json")
firebase_admin.initialize_app(cred)

stripe.api_key = os.getenv("STRIPE_SECRET_KEY")

app = FastAPI(title="Rizix API", version="2.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(market_router)
app.include_router(billing_router)
from api.routes_ml import router as ml_router
app.include_router(ml_router)

@app.get("/")
def health_check():
    return {"message": "Rizix API працює 🚀"}