import { expect, type Locator, type Page } from '@playwright/test';

export class InvoiceApprovalPage {
  constructor(private readonly page: Page) {}

  async signIn(username: string, password: string): Promise<void> {
    await this.page.getByLabel('Username').fill(username);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Sign in' }).click();
    await expect(this.page.getByRole('tab', { name: 'Invoices' })).toBeVisible();
  }

  async createAndSubmitInvoice(customerName: string, amount: number): Promise<void> {
    await this.page.getByLabel('Customer name').fill(customerName);
    await this.page.getByLabel('Amount').fill(String(amount));
    await this.page.getByRole('button', { name: 'Create invoice' }).click();

    const row = this.invoiceRow(customerName);
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: /^Submit invoice .* for approval$/ }).click();
    await expect(this.statusFor(customerName)).toHaveText('Pending Approval');
  }

  async openApprovals(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
  }

  async openInvoices(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Invoices' }).click();
  }

  async approveInvoice(customerName: string): Promise<void> {
    const pendingInvoice = this.page.getByRole('listitem').filter({ hasText: customerName });
    await expect(pendingInvoice).toBeVisible();
    await pendingInvoice.getByRole('button', { name: /^Approve invoice / }).click();
  }

  decisionError(): Locator {
    return this.page.getByRole('tabpanel', { name: 'Approvals' }).getByRole('alert');
  }

  statusFor(customerName: string): Locator {
    return this.invoiceRow(customerName).getByRole('cell').nth(3);
  }

  private invoiceRow(customerName: string): Locator {
    return this.page.getByRole('row').filter({
      has: this.page.getByRole('cell', { name: customerName, exact: true }),
    });
  }
}
