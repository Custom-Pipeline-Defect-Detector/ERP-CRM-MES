import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { crmApi, erpApi } from '../../lib/api'
import { formatCurrency, formatNumber } from '../../lib/formatters'
import type { CustomerListItem, ProductListItem, SalesOrderListItem } from '../../types/runtime'

const defaultCustomerForm = {
  code: '',
  name: '',
  segment: 'KeyAccount',
  region: 'Shandong',
  industry: 'Solar',
  creditRating: 'A',
}

export function CrmModulePage() {
  const { session } = useSession()
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [salesOrders, setSalesOrders] = useState<SalesOrderListItem[]>([])
  const [products, setProducts] = useState<ProductListItem[]>([])
  const [customerForm, setCustomerForm] = useState(defaultCustomerForm)
  const [salesOrderForm, setSalesOrderForm] = useState({
    customerId: '',
    productCode: '',
    quantity: '40',
    unitPrice: '780',
    requestedDeliveryDate: '2026-05-10',
  })
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  async function refreshData() {
    setIsLoading(true)

    try {
      const [customersResponse, salesOrdersResponse, productsResponse] = await Promise.all([
        crmApi.getCustomers(),
        crmApi.getSalesOrders(),
        erpApi.getProducts(),
      ])

      setCustomers(customersResponse)
      setSalesOrders(salesOrdersResponse)
      setProducts(productsResponse)
      setError(null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!session) {
      return
    }

    void refreshData()
  }, [session?.token])

  useEffect(() => {
    if (customers.length > 0 && !salesOrderForm.customerId) {
      setSalesOrderForm((current) => ({ ...current, customerId: customers[0].id }))
    }
  }, [customers, salesOrderForm.customerId])

  useEffect(() => {
    if (products.length > 0 && !salesOrderForm.productCode) {
      setSalesOrderForm((current) => ({ ...current, productCode: products[0].code }))
    }
  }, [products, salesOrderForm.productCode])

  async function handleCreateCustomer() {
    setIsSaving(true)

    try {
      await crmApi.createCustomer(customerForm)
      setCustomerForm(defaultCustomerForm)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCreateSalesOrder() {
    setIsSaving(true)

    try {
      await crmApi.createSalesOrder({
        customerId: salesOrderForm.customerId,
        productCode: salesOrderForm.productCode,
        quantity: Number(salesOrderForm.quantity),
        unitPrice: Number(salesOrderForm.unitPrice),
        requestedDeliveryDate: salesOrderForm.requestedDeliveryDate,
      })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleSalesOrderAction(
    salesOrderId: string,
    action: 'submit' | 'approve' | 'release',
  ) {
    setIsSaving(true)

    try {
      if (action === 'submit') {
        await crmApi.submitSalesOrder(salesOrderId)
      }

      if (action === 'approve') {
        await crmApi.approveSalesOrder(salesOrderId)
      }

      if (action === 'release') {
        await crmApi.releaseSalesOrder(salesOrderId)
      }

      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">CRM</span>
          <span className="tag-chip tag-chip-blue">Lead-to-Order</span>
        </div>
        <h1 className="hero-title">Own customer truth and release commercial demand correctly.</h1>
        <p className="hero-copy">
          Customers and sales orders are live now. Releasing a sales order creates a linked ERP
          production order instead of stopping at document entry.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Create Customer</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Code</span>
              <input
                value={customerForm.code}
                onChange={(event) => setCustomerForm((current) => ({ ...current, code: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>Name</span>
              <input
                value={customerForm.name}
                onChange={(event) => setCustomerForm((current) => ({ ...current, name: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>Segment</span>
              <select
                value={customerForm.segment}
                onChange={(event) =>
                  setCustomerForm((current) => ({ ...current, segment: event.target.value }))
                }
              >
                <option value="Strategic">Strategic</option>
                <option value="KeyAccount">Key Account</option>
                <option value="Distributor">Distributor</option>
                <option value="Standard">Standard</option>
                <option value="ServiceOnly">Service Only</option>
              </select>
            </label>
            <label className="form-field">
              <span>Region</span>
              <input
                value={customerForm.region}
                onChange={(event) => setCustomerForm((current) => ({ ...current, region: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>Industry</span>
              <input
                value={customerForm.industry}
                onChange={(event) =>
                  setCustomerForm((current) => ({ ...current, industry: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Credit</span>
              <input
                value={customerForm.creditRating}
                onChange={(event) =>
                  setCustomerForm((current) => ({ ...current, creditRating: event.target.value }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving} onClick={() => void handleCreateCustomer()} type="button">
            Add customer
          </button>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Create Sales Order</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Customer</span>
              <select
                value={salesOrderForm.customerId}
                onChange={(event) =>
                  setSalesOrderForm((current) => ({ ...current, customerId: event.target.value }))
                }
              >
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.code} | {customer.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Product</span>
              <select
                value={salesOrderForm.productCode}
                onChange={(event) =>
                  setSalesOrderForm((current) => ({ ...current, productCode: event.target.value }))
                }
              >
                {products.map((product) => (
                  <option key={product.id} value={product.code}>
                    {product.code} | {product.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Quantity</span>
              <input
                type="number"
                value={salesOrderForm.quantity}
                onChange={(event) =>
                  setSalesOrderForm((current) => ({ ...current, quantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Unit price</span>
              <input
                type="number"
                value={salesOrderForm.unitPrice}
                onChange={(event) =>
                  setSalesOrderForm((current) => ({ ...current, unitPrice: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Requested delivery</span>
              <input
                type="date"
                value={salesOrderForm.requestedDeliveryDate}
                onChange={(event) =>
                  setSalesOrderForm((current) => ({
                    ...current,
                    requestedDeliveryDate: event.target.value,
                  }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving} onClick={() => void handleCreateSalesOrder()} type="button">
            Add sales order
          </button>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Customers</h3>
        {isLoading ? (
          <p className="muted-copy">Loading CRM data.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Customer</th>
                  <th>Segment</th>
                  <th>Region</th>
                  <th>Industry</th>
                  <th>Credit</th>
                </tr>
              </thead>
              <tbody>
                {customers.map((customer) => (
                  <tr key={customer.id}>
                    <td>
                      <strong>{customer.code}</strong>
                      <div className="cell-meta">{customer.name}</div>
                    </td>
                    <td>{customer.segment}</td>
                    <td>{customer.region}</td>
                    <td>{customer.industry}</td>
                    <td>{customer.creditRating}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Sales Orders</h3>
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Order</th>
                <th>Customer</th>
                <th>Product</th>
                <th>Quantity</th>
                <th>Amount</th>
                <th>State</th>
                <th>ERP Link</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {salesOrders.map((salesOrder) => (
                <tr key={salesOrder.id}>
                  <td>{salesOrder.orderNumber}</td>
                  <td>{salesOrder.customerName}</td>
                  <td>{salesOrder.productCode}</td>
                  <td>{formatNumber(salesOrder.quantity)}</td>
                  <td>{formatCurrency(salesOrder.totalAmount)}</td>
                  <td>
                    <span className="status-pill">{salesOrder.state}</span>
                  </td>
                  <td>{salesOrder.productionOrderNumber ?? 'Not released'}</td>
                  <td>
                    <div className="action-row">
                      <button
                        className="button button-secondary"
                        disabled={isSaving || salesOrder.state !== 'Draft'}
                        onClick={() => void handleSalesOrderAction(salesOrder.id, 'submit')}
                        type="button"
                      >
                        Submit
                      </button>
                      <button
                        className="button button-secondary"
                        disabled={isSaving || salesOrder.state !== 'Submitted'}
                        onClick={() => void handleSalesOrderAction(salesOrder.id, 'approve')}
                        type="button"
                      >
                        Approve
                      </button>
                      <button
                        className="button button-primary"
                        disabled={isSaving || salesOrder.state !== 'Approved'}
                        onClick={() => void handleSalesOrderAction(salesOrder.id, 'release')}
                        type="button"
                      >
                        Release
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
