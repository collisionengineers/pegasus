import { TableWrap } from '@pegasus/design-system';

/** A hand-written three-column table inside the bordered, horizontally scrolling wrapper. */
export const HandWrittenTable = () => (
  <div style={{ maxWidth: 720 }}>
    <TableWrap>
      <table>
        <caption className="vh">Principals</caption>
        <thead>
          <tr>
            <th scope="col">Principal</th>
            <th scope="col">Open cases</th>
            <th scope="col">Last instruction</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>
              <a href="#">AXA</a>
            </td>
            <td className="tabular">42</td>
            <td>
              <time>14 Aug 08:52</time>
            </td>
          </tr>
          <tr>
            <td>
              <a href="#">Direct Line</a>
            </td>
            <td className="tabular">17</td>
            <td>
              <time>13 Aug 16:40</time>
            </td>
          </tr>
          <tr>
            <td>
              <a href="#">Aviva</a>
            </td>
            <td className="tabular">9</td>
            <td>
              <time>12 Aug 09:14</time>
            </td>
          </tr>
        </tbody>
      </table>
    </TableWrap>
  </div>
);
