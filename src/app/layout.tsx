import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Kwarta — Your money, made clear",
  description: "A simple, private money tracker that runs on your laptop.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
