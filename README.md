# Kwarta

A private personal money tracker built with Next.js, Tailwind CSS, ASP.NET Core, and SQLite. Both services run on your laptop; the database is stored in `api/data/kwarta.db`.

## What it does

- Tracks opening capital, paychecks, and everyday spending.
- Groups expenses into needs and wants, with categories such as food, transport, and bills.
- Tracks installment plans with monthly or every-15-day payment schedules, payments made, and remaining balance.
- Lets you record salary at a manually chosen date and amount, or keep a 15-day payday reminder. Take-home pay is never assumed to be a fixed amount.
- Includes a persistent light/dark theme switch and quick Housing, Food, and other expense category buttons.
- Shows current balance, monthly totals, a six-month income/spending chart, category breakdown, recent activity, and upcoming payments.
- Formats values in Philippine pesos.

Tracking begins in November 2026. The first API launch creates an empty local database with zero opening capital and manual payday entry; no demo transactions or installment plans are inserted. Set your capital and payday schedule in Settings, then add your own transactions and plans. The app does not yet include deleting individual entries.

## Requirements

- Node.js 20 or newer and npm.
- .NET 8 SDK.

## Run locally

Open a terminal in this folder and start the API:

```powershell
dotnet run --project api
```

If Windows reports that an application-control policy blocked the API executable or DLL, changing the `dotnet run` command will not resolve the policy. Check the Windows Code Integrity or AppLocker event logs for the block details, then ask the device administrator to authorize the project or its trusted build output. Do not disable application-control protections to work around the block.

In a second terminal, install the web app packages and start Next.js:

```powershell
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). The API listens at `http://localhost:5055`. Its health check is `http://localhost:5055/api/health`.

After changing API code, stop the running API with Ctrl+C and run `dotnet run --project api` again. `dotnet run` does not hot-reload endpoint changes.

The API applies the SQLite schema migration at startup and stores the database at `api/data/kwarta.db`. Keep that file to retain your data. Back it up by copying it while both services are stopped. The dashboard history and entry forms begin on November 1, 2026.

## Local API

- `GET /api/dashboard?month=YYYY-MM`
- `GET /api/transactions?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET /api/transactions/{id}`
- `POST /api/transactions`
- `PUT /api/transactions/{id}`
- `DELETE /api/transactions/{id}`
- `GET /api/installments`
- `POST /api/installments`
- `POST /api/installments/{id}/payments`
- `GET /api/settings`
- `PUT /api/settings/capital`
- `PUT /api/settings/salary-schedule`

There is no sign-in or external service. CORS is limited to the local Next.js development origins.
