# Kwarta

A private personal money tracker built with Next.js, Tailwind CSS, ASP.NET Core, and SQLite. Both services run on your laptop; the database is stored in `api/data/kwarta.db`.

## What it does

- Tracks opening capital, paychecks, and everyday spending.
- Groups expenses into needs and wants, with categories such as food, transport, and bills.
- Tracks installment plans, monthly due dates, payments made, and remaining balance.
- Shows current balance, monthly totals, a six-month income/spending chart, category breakdown, recent activity, and upcoming payments.
- Formats values in Philippine pesos.

The first API launch creates a local database and adds example records so the dashboard has something to show. Set your own opening capital by clicking the available balance card or Settings. You can remove the example database before first launch if you prefer a blank start: delete `api/data/kwarta.db`; the next API launch will create a fresh example database. This sample version does not yet include deleting individual entries.

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

The API applies the SQLite schema migration at startup and stores the database at `api/data/kwarta.db`. Keep that file to retain your data. Back it up by copying it while both services are stopped.

## Local API

- `GET /api/dashboard?month=YYYY-MM`
- `GET /api/transactions?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `POST /api/transactions`
- `GET /api/installments`
- `POST /api/installments`
- `POST /api/installments/{id}/payments`
- `GET /api/settings`
- `PUT /api/settings/capital`

There is no sign-in or external service. CORS is limited to the local Next.js development origins.
